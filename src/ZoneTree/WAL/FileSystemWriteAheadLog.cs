using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/
public sealed class FileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    volatile bool IsDisposed;

    readonly IFileStreamProvider FileStreamProvider;

    readonly IFileStream FileStream;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    public string FilePath { get; }

    public bool EnableIncrementalBackup { get; set; }

    public FileSystemWriteAheadLog(
        IFileStreamProvider fileStreamProvider,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        int writeBufferSize = 4096)
    {
        FilePath = filePath;
        FileStream = fileStreamProvider.CreateFileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, writeBufferSize);
        FileStream.Seek(0, SeekOrigin.End);
        FileStreamProvider = fileStreamProvider;
        KeySerializer = keySerializer;
        ValueSerializer = valueSerializer;
    }

    public void Append(in TKey key, in TValue value, long opIndex)
    {
        var keyBytes = KeySerializer.Serialize(key);
        var valueBytes = ValueSerializer.Serialize(value);
        lock (this)
        {
            AppendLogEntry(keyBytes, valueBytes, opIndex);
        }
    }

    public void Drop()
    {
        lock (this)
        {
            if (!IsDisposed)
            {
                FileStream.Dispose();
                IsDisposed = true;
            }
            FileStreamProvider.DeleteFile(FilePath);
        }
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
            crc32 = Crc32Computer.Compute(crc32, (ulong)OpIndex);
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

    void AppendLogEntry(byte[] keyBytes, byte[] valueBytes, long opIndex)
    {
        var entry = new LogEntry
        {
            OpIndex = opIndex,
            KeyLength = keyBytes.Length,
            ValueLength = valueBytes.Length,
            Key = keyBytes,
            Value = valueBytes
        };
        entry.Checksum = entry.CreateChecksum();

        var binaryWriter = new BinaryWriter(FileStream.ToStream());
        binaryWriter.Write(entry.OpIndex);
        binaryWriter.Write(entry.KeyLength);
        binaryWriter.Write(entry.ValueLength);
        if (entry.Key != null)
            binaryWriter.Write(entry.Key);
        if (entry.Value != null)
            binaryWriter.Write(entry.Value);
        binaryWriter.Write(entry.Checksum);
        Flush();
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
            FileStream.ToStream(),
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
        if (IsDisposed)
            return;
        lock (this)
        {
            if (IsDisposed)
                return;
            Flush();
            FileStream.Dispose();
            IsDisposed = true;
        }
    }

    private void Flush()
    {
        if (!IsDisposed)
            FileStream.Flush(true);
    }

    public long ReplaceWriteAheadLog(TKey[] keys, TValue[] values, bool disableBackup)
    {
        lock (this)
        {
            if (!disableBackup && EnableIncrementalBackup)
            {
                IncrementalLogAppender
                    .AppendLogToTheBackupFile(
                        FileStreamProvider,
                        FilePath + ".full",
                        () =>
                        {
                            FileStream.Flush(true);
                            FileStream.Seek(0, SeekOrigin.Begin);
                            var existingLength = FileStream.Length;
                            var bytes = new byte[existingLength];
                            FileStream.Read(bytes);
                            return bytes;
                        });
            }

            // Todo: Implement replacement crash recovery.
            // 1. Take backup of the existing WAL
            // 2. Replace the current WAL
            // 3. Delete the backup
            // 4. Add backup recovery to the constructor if one exists.

            var existingLength = FileStream.Length;
            FileStream.SetLength(0);

            var len = keys.Length;
            for (var i = 0; i < len; ++i)
            {
                Append(keys[i], values[i], i);
            }
            var diff = existingLength - FileStream.Length;
            return diff;
        }
    }

    public void MarkFrozen()
    {
        Task.Run(() =>
        {
            Dispose();
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
