﻿using System.Collections.Concurrent;
using System.Text;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Exceptions.WAL;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.WAL;

public sealed class AsyncCompressedFileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly ILogger Logger;

    readonly IFileStreamProvider FileStreamProvider;

    readonly CompressedFileStream FileStream;

    readonly BinaryWriter BinaryWriter;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    readonly int EmptyQueuePollInterval;

    readonly ConcurrentQueue<QueueItem> Queue = new();

    public struct QueueItem
    {
        public TKey Key;

        public TValue Value;

        public long OpIndex;

        public QueueItem(in TKey key, in TValue value, long opIndex)
        {
            Key = key;
            Value = value;
            OpIndex = opIndex;
        }
    }

    readonly object AppendLock = new();

    volatile bool isRunning = false;

    volatile bool isWriterCancelled = false;

    volatile Task WriteTask;

    public string FilePath { get; }

    public bool EnableIncrementalBackup { get; set; }

    public AsyncCompressedFileSystemWriteAheadLog(
        ILogger logger,
        IFileStreamProvider fileStreamProvider,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        WriteAheadLogOptions options)
    {
        Logger = logger;
        FilePath = filePath;
        EmptyQueuePollInterval = options.AsyncCompressedModeOptions.EmptyQueuePollInterval;
        FileStream = new CompressedFileStream(
            Logger,
            fileStreamProvider,
            filePath,
            options.CompressionBlockSize,
            false,
            0,
            options.CompressionMethod,
            options.CompressionLevel);
        BinaryWriter = new BinaryWriter(FileStream, Encoding.UTF8, true);
        FileStream.Seek(0, SeekOrigin.End);
        FileStreamProvider = fileStreamProvider;
        KeySerializer = keySerializer;
        ValueSerializer = valueSerializer;
        StartWriter();
    }

    void StartWriter()
    {
        StopWriter(false);
        isRunning = true;
        WriteTask = Task.Factory.StartNew(() => DoWrite(), TaskCreationOptions.LongRunning);
    }

    void StopWriter(bool consumeAll)
    {
        isRunning = false;
        WriteTask?.Wait();
        WriteTask = null;
        if (consumeAll)
            ConsumeQueue();
    }

    void CancelWriter()
    {
        lock (AppendLock)
        {
            isWriterCancelled = true;
        }
    }

    void ConsumeQueue()
    {
        lock (this)
        {
            while (Queue.TryDequeue(out var q))
            {
                var keyBytes = KeySerializer.Serialize(q.Key);
                var valueBytes = ValueSerializer.Serialize(q.Value);
                AppendLogEntry(keyBytes, valueBytes, q.OpIndex);
            }
        }
        FileStream.WriteTail();
    }

    void DoWrite()
    {
        while (isRunning)
        {
            while (isRunning && Queue.TryDequeue(out var q))
            {
                try
                {
                    var keyBytes = KeySerializer.Serialize(q.Key);
                    var valueBytes = ValueSerializer.Serialize(q.Value);
                    AppendLogEntry(keyBytes, valueBytes, q.OpIndex);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
            }
            if (isRunning && Queue.IsEmpty)
            {
                try
                {
                    FileStream.WriteTail();
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }
                if (!isRunning)
                    break;
                if (EmptyQueuePollInterval == 0)
                    Thread.Yield();
                else
                    Thread.Sleep(EmptyQueuePollInterval);
            }
        }
    }

    public void Append(in TKey key, in TValue value, long opIndex)
    {
        Queue.Enqueue(new QueueItem(in key, in value, opIndex));
    }

    public void Drop()
    {
        Queue.Clear();
        StopWriter(false);
        FileStream.Dispose();
        if (FileStreamProvider.FileExists(FilePath))
            FileStreamProvider.DeleteFile(FilePath);
        var tailPath = FilePath + ".tail";
        if (FileStreamProvider.FileExists(tailPath))
            FileStreamProvider.DeleteFile(tailPath);
    }

    struct LogEntry
    {
        public long OpIndex;
        public int KeyLength;
        public int ValueLength;
        public byte[] Key;
        public byte[] Value;
        public uint Checksum;

        public uint CreateChecksum()
        {
            uint crc32 = 0;
            if (Crc32Computer_SSE42_X64.IsSupported)
            {
                crc32 = Crc32Computer_SSE42_X64.Compute(crc32, (ulong)OpIndex);
                crc32 = Crc32Computer_SSE42_X64.Compute(crc32, KeyLength);
                crc32 = Crc32Computer_SSE42_X64.Compute(crc32, ValueLength);
                crc32 = Crc32Computer_SSE42_X64.Compute(crc32, Key);
                crc32 = Crc32Computer_SSE42_X64.Compute(crc32, Value);
                return crc32;
            }

            if (Crc32Computer_ARM64.IsSupported)
            {
                crc32 = Crc32Computer_ARM64.Compute(crc32, (ulong)OpIndex);
                crc32 = Crc32Computer_ARM64.Compute(crc32, KeyLength);
                crc32 = Crc32Computer_ARM64.Compute(crc32, ValueLength);
                crc32 = Crc32Computer_ARM64.Compute(crc32, Key);
                crc32 = Crc32Computer_ARM64.Compute(crc32, Value);
                return crc32;
            }
            throw new PlatformNotSupportedException();
        }

        public bool ValidateChecksum()
        {
            return CreateChecksum() == Checksum;
        }
    }

    void AppendLogEntry(byte[] keyBytes, byte[] valueBytes, long opIndex)
    {
        lock (AppendLock)
        {
            if (isWriterCancelled)
                return;
            var entry = new LogEntry
            {
                OpIndex = opIndex,
                KeyLength = keyBytes.Length,
                ValueLength = valueBytes.Length,
                Key = keyBytes,
                Value = valueBytes
            };
            entry.Checksum = entry.CreateChecksum();

            var binaryWriter = BinaryWriter;
            binaryWriter.Write(entry.OpIndex);
            binaryWriter.Write(entry.KeyLength);
            binaryWriter.Write(entry.ValueLength);
            if (entry.Key != null)
                binaryWriter.Write(entry.Key);
            if (entry.Value != null)
                binaryWriter.Write(entry.Value);
            binaryWriter.Write(entry.Checksum);
            binaryWriter.Flush();
        }
    }

    static void ReadLogEntry(BinaryReader reader, ref LogEntry entry)
    {
        entry.OpIndex = reader.ReadInt64();
        entry.KeyLength = reader.ReadInt32();
        entry.ValueLength = reader.ReadInt32();
        entry.Key = reader.ReadBytes(entry.KeyLength);
        entry.Value = reader.ReadBytes(entry.ValueLength);
        entry.Checksum = reader.ReadUInt32();
    }

    public WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure,
        bool sortByOpIndexes)
    {
        return WriteAheadLogEntryReader.ReadLogEntries<TKey, TValue, LogEntry>(
            Logger,
            FileStream,
            stopReadOnException,
            stopReadOnChecksumFailure,
            ReadLogEntry,
            DeserializeLogEntry,
            sortByOpIndexes);
    }

    (bool isValid, TKey key, TValue value, long opIndex) DeserializeLogEntry(in LogEntry logEntry)
    {
        var isValid = logEntry.ValidateChecksum();
        var key = KeySerializer.Deserialize(logEntry.Key);
        var value = ValueSerializer.Deserialize(logEntry.Value);
        return (isValid, key, value, logEntry.OpIndex);
    }

    public void Dispose()
    {
        StopWriter(true);
        FileStream.Dispose();
    }

    public long ReplaceWriteAheadLog(TKey[] keys, TValue[] values, bool disableBackup)
    {
        lock (this)
        {
            if (!disableBackup && EnableIncrementalBackup)
            {
                StopWriter(true);
                IncrementalLogAppender
                    .AppendLogToTheBackupFile(
                        FileStreamProvider,
                        FilePath + ".full",
                        () =>
                        {
                            return FileStream.GetFileContentIncludingTail();
                        });
            }
            else
            {
                Queue.Clear();
                CancelWriter();
            }
            // Replacement crash recovery is not required here,
            // because the async-compressed write ahead log is not durable.
            // implementing crash recovery here does not make it durable.
            var existingLength = FileStream.Length;
            FileStream.SetLength(0);
            if (isWriterCancelled)
                isWriterCancelled = false;
            else
                StartWriter();
            var len = keys.Length;
            for (var i = 0; i < len; ++i)
            {
                Queue.Enqueue(new QueueItem(in keys[i], in values[i], 0));
            }
            return 0;
        }
    }

    public void MarkFrozen()
    {
        Task.Run(() =>
        {
            try
            {
                StopWriter(true);
                FileStream.Dispose();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        });
    }

    public void TruncateIncompleteTailRecord(IncompleteTailRecordFoundException incompleteTailException)
    {
        lock (this)
        {
            FileStream.SetLength(incompleteTailException.RecordPosition);
        }
    }
}
