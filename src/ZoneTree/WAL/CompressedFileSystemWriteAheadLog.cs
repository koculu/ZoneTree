using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;

namespace Tenray.ZoneTree.WAL;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/
public sealed class CompressedFileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly CompressedFileStream FileStream;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    public string FilePath { get; }

    public bool EnableIncrementalBackup { get; set; }

    public CompressedFileSystemWriteAheadLog(
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        int compressionBlockSize)
    {
        FilePath = filePath;
        FileStream = new CompressedFileStream(filePath, compressionBlockSize);
        KeySerializer = keySerializer;
        ValueSerializer = valueSerializer;
    }

    public void Append(in TKey key, in TValue value)
    {
        var keyBytes = KeySerializer.Serialize(key);
        var valueBytes = ValueSerializer.Serialize(value);
        lock (this)
        {
            AppendLogEntry(keyBytes, valueBytes);
        }
    }

    public void Drop()
    {
        FileStream.Dispose();
        File.Delete(FilePath);
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
        FileStream.Dispose();
    }

    public long ReplaceWriteAheadLog(TKey[] keys, TValue[] values, bool disableBackup)
    {
        lock (this)
        {
            if (!disableBackup && EnableIncrementalBackup)
                AppendCurrentWalToTheFullLog();

            var existingLength = (int)FileStream.Length;
            FileStream.SetLength(0);

            var len = keys.Length;
            for (var i = 0; i < len; ++i)
            {
                Append(keys[i], values[i]);
            }
            var diff = existingLength - FileStream.Length;
            return diff;
        }
    }

    void AppendCurrentWalToTheFullLog()
    {
        var backupFile = FilePath + ".full";
        var backupDataOffset = sizeof(long) * 3;
        using var fs = new FileStream(
                backupFile,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                4096, false);
        var br = new BinaryReader(fs);
        bool hasLengthStamp = fs.Length > backupDataOffset;
        if (hasLengthStamp)
        {
            // read the length-stamp three times and select the most common one,
            // to ensure the length was not written partially to the file.
            var lengthInTheFile1 = br.ReadInt64();
            var lengthInTheFile2 = br.ReadInt64();
            var lengthInTheFile3 = br.ReadInt64();
            if (lengthInTheFile1 != lengthInTheFile2)
            {
                if (lengthInTheFile1 == lengthInTheFile3)
                    lengthInTheFile1 = lengthInTheFile3;
                else if (lengthInTheFile2 == lengthInTheFile3)
                    lengthInTheFile1 = lengthInTheFile2;
                else
                {
                    // 3 length-stamps are different from each other.
                    // We might copy the corrupted file to a backup location
                    // and start a new backup file immediately
                    // to not to interrupt the system on full log corruption.
                    // For now, we prefer throwing an exception.
                    throw new WriteAheadLogFullLogCorruptionException(backupFile);
                }
            }

            // make sure the file has no crashed backup data.
            if (fs.Length > lengthInTheFile1)
            {
                fs.SetLength(lengthInTheFile1);
            }
        }
        else
        {
            fs.SetLength(0);
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Flush();
        }

        // first append the additional data.
        FileStream.SealStream();
        var bytes = FileStream.GetFileContent();

        fs.Seek(0, SeekOrigin.End);
        fs.Write(bytes);
        fs.Flush();
        
        // now write the file length-stamps.
        // what happens if a crash happens with partial write of the fs Length?
        // To prevent that, we write and flush the length-stamp three times with separate flushes..
        fs.Position = 0;
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush();
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush();
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush();
    }

    public void MarkFrozen()
    {
        FileStream.SealStream();
        FileStream.Dispose();
    }
}
