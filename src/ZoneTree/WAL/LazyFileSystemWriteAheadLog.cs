using System.Collections.Concurrent;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Exceptions.WAL;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.WAL;

public sealed class LazyFileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly CompressedFileStream FileStream;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;
    
    readonly int EmptyQueuePollInterval;

    readonly ConcurrentQueue<CombinedValue<TKey, TValue>> Queue = new();

    volatile bool isRunning = false;

    volatile Task WriteTask;

    public string FilePath { get; }

    public bool EnableIncrementalBackup { get; set; }

    public LazyFileSystemWriteAheadLog(
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        int compressionBlockSize,
        int emptyQueuePollInterval)
    {
        FilePath = filePath;
        EmptyQueuePollInterval = emptyQueuePollInterval;
        FileStream = new CompressedFileStream(
            filePath,
            compressionBlockSize,
            false,
            0);
        FileStream.Seek(0, SeekOrigin.End);
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

    void ConsumeQueue()
    {
        while (Queue.TryDequeue(out var q))
        {
            var keyBytes = KeySerializer.Serialize(q.Value1);
            var valueBytes = ValueSerializer.Serialize(q.Value2);
            AppendLogEntry(keyBytes, valueBytes);
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
                    var keyBytes = KeySerializer.Serialize(q.Value1);
                    var valueBytes = ValueSerializer.Serialize(q.Value2);
                    AppendLogEntry(keyBytes, valueBytes);
                }
                catch (Exception e)
                {
                    // todo log the exception.
                    //Console.WriteLine(e.Message);
                }
            }
            if (isRunning && Queue.IsEmpty)
            {
                try
                {
                    FileStream.WriteTail();
                }
                catch(Exception e)
                {
                    // todo log the exception.
                    // Console.WriteLine(e.Message);
                }
                Thread.Sleep(EmptyQueuePollInterval);
            }
        }
    }

    public void Append(in TKey key, in TValue value)
    {
        lock (this)
        {
            Queue.Enqueue(new CombinedValue<TKey, TValue>(in key, in value));
        }
    }

    public void Drop()
    {
        Queue.Clear();
        StopWriter(false);
        FileStream.Dispose();
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        var tailPath = FilePath + ".tail";
        if (File.Exists(tailPath))
            File.Delete(tailPath);
    }

    struct LogEntry
    {
        public int KeyLength;
        public int ValueLength;
        public byte[] Key;
        public byte[] Value;
        public uint Checksum;

        public uint CreateChecksum()
        {
            uint crc32 = 0;
            crc32 = Crc32Computer.Compute(crc32, KeyLength);
            crc32 = Crc32Computer.Compute(crc32, ValueLength);
            crc32 = Crc32Computer.Compute(crc32, Key);
            crc32 = Crc32Computer.Compute(crc32, Value);
            return crc32;
        }

        public bool ValidateChecksum()
        {
            return CreateChecksum() == Checksum;
        }
    }

    void AppendLogEntry(byte[] keyBytes, byte[] valueBytes)
    {
        var entry = new LogEntry
        {
            KeyLength = keyBytes.Length,
            ValueLength = valueBytes.Length,
            Key = keyBytes,
            Value = valueBytes
        };
        entry.Checksum = entry.CreateChecksum();

        var binaryWriter = new BinaryWriter(FileStream);
        binaryWriter.Write(entry.KeyLength);
        binaryWriter.Write(entry.ValueLength);
        if (entry.Key != null)
            binaryWriter.Write(entry.Key);
        if (entry.Value != null)
            binaryWriter.Write(entry.Value);
        binaryWriter.Write(entry.Checksum);
    }

    static void ReadLogEntry(BinaryReader reader, ref LogEntry entry)
    {
        entry.KeyLength = reader.ReadInt32();
        entry.ValueLength = reader.ReadInt32();
        entry.Key = reader.ReadBytes(entry.KeyLength);
        entry.Value = reader.ReadBytes(entry.ValueLength);
        entry.Checksum = reader.ReadUInt32();
    }

    public WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure)
    {
        return WriteAheadLogEntryReader.ReadLogEntries<TKey, TValue, LogEntry>(
            FileStream,
            stopReadOnException,
            stopReadOnChecksumFailure,
            ReadLogEntry,
            DeserializeLogEntry);
    }

    (bool isValid, TKey key, TValue value) DeserializeLogEntry(in LogEntry logEntry)
    {
        var isValid = logEntry.ValidateChecksum();
        var key = KeySerializer.Deserialize(logEntry.Key);
        var value = ValueSerializer.Deserialize(logEntry.Value);
        return (isValid, key, value);
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
                ConsumeQueue();
                IncrementalLogAppender
                    .AppendLogToTheBackupFile(
                        FilePath + ".full",
                        () =>
                        {
                            return FileStream.GetFileContentIncludingTail();
                        });
            }
            else
            {
                StopWriter(false);
                Queue.Clear();
            }
            // Replacement crash recovery is not required here,
            // because the lazy write ahead log is not durable.
            // implementing crash recovery here does not make it durable.
            var existingLength = FileStream.Length;
            FileStream.SetLength(0);
            StartWriter();
            var len = keys.Length;
            for (var i = 0; i < len; ++i)
            {
                Queue.Enqueue(new CombinedValue<TKey, TValue>(in keys[i], in values[i]));
            }
            return 0;
        }
    }

    public void MarkFrozen()
    {
        StopWriter(true);
        FileStream.WriteTail();
        FileStream.Dispose();
    }

    public void TruncateIncompleteTailRecord(IncompleteTailRecordFoundException incompleteTailException)
    {
        lock (this)
        {
            FileStream.SetLength(incompleteTailException.RecordPosition);
        }
    }
}
