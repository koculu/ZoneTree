using System.Text;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/
public sealed class CompressedFileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly IFileStreamProvider FileStreamProvider;

    CompressedFileStream FileStream;

    BinaryWriter BinaryWriter;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    public string FilePath { get; }
    
    public int CompressionBlockSize { get; }
    
    public bool EnableTailWriterJob { get; }
    
    public int TailWriterJobInterval { get; }
    
    public bool EnableIncrementalBackup { get; set; }

    public CompressedFileSystemWriteAheadLog(
        IFileStreamProvider fileStreamProvider,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        int compressionBlockSize,
        bool enableTailWriterJob,
        int tailWriterJobInterval)
    {
        FileStreamProvider = fileStreamProvider;
        FilePath = filePath;
        CompressionBlockSize = compressionBlockSize;
        EnableTailWriterJob = enableTailWriterJob;
        TailWriterJobInterval = tailWriterJobInterval;
        KeySerializer = keySerializer;
        ValueSerializer = valueSerializer;
        CreateFileStream();
    }

    void CreateFileStream()
    {
        FileStream = new CompressedFileStream(
            FileStreamProvider,
            FilePath,
            CompressionBlockSize,
            EnableTailWriterJob,
            TailWriterJobInterval);
        BinaryWriter = new BinaryWriter(FileStream, Encoding.UTF8, true);
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
        FileStream.Dispose();
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
                            return FileStream.GetFileContentIncludingTail();
                        });
            }

            var existingLength = FileStream.Length;
            long diff = 0;
            try
            {
                // Replacement crash recovery:
                // 1. Write keys and values to the tmp file.
                // 2. Use Replace API to replace target file.

                FileStream.Dispose();
                BinaryWriter = null;

                var tmpFilePath = FilePath + ".tmp";
                var tmpTailFilePath = FilePath + ".tmp.tail";
                var existingFileStream = FileStream;
                using (var tmpFileStream = new CompressedFileStream(
                    FileStreamProvider,
                    FilePath,
                    CompressionBlockSize,
                    EnableTailWriterJob,
                    TailWriterJobInterval))
                {
                    FileStream = tmpFileStream;
                    BinaryWriter = new BinaryWriter(tmpFileStream, Encoding.UTF8, true);
                    tmpFileStream.SetLength(0);
                    var len = keys.Length;
                    for (var i = 0; i < len; ++i)
                    {
                        Append(keys[i], values[i], i);
                    }
                    diff = existingLength - FileStream.Length;
                    FileStream = null;
                }

                // CompressedFileStream tail does not have 100% durability.
                // Therefore implementing replacement for tail with crash-resilience
                // is not necessary.
                FileStreamProvider.Replace(tmpFilePath, FilePath, null);
                FileStreamProvider.Replace(tmpTailFilePath, FilePath + ".tail", null);
                CreateFileStream();
            }
            catch (Exception)
            {
                FileStream?.Dispose();
                CreateFileStream();
                diff = existingLength - FileStream.Length;
            }
            finally
            {
                if (FileStream == null)
                    CreateFileStream();
                else if(FileStream.FilePath != FilePath)
                {
                    FileStream?.Dispose();
                    CreateFileStream();
                }
            }
            return diff;
        }
    }

    public void MarkFrozen()
    {
        Task.Run(() =>
        {
            FileStream.Dispose();
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
