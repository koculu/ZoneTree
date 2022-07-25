using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;

namespace Tenray.ZoneTree.WAL;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/
public sealed class FileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly FileStream FileStream;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    public string FilePath { get; }

    public bool EnableIncrementalBackup { get; set; }

    public FileSystemWriteAheadLog(
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string filePath,
        int writeBufferSize = 4096)
    {
        FilePath = filePath;
        FileStream = new FileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, writeBufferSize, false);
        FileStream.Seek(0, SeekOrigin.End);
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

    private void AppendLogEntry(byte[] keyBytes, byte[] valueBytes)
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
        Flush();
    }

    private static LogEntry ReadLogEntry(BinaryReader reader, ref LogEntry entry)
    {
        entry.KeyLength = reader.ReadInt32();
        entry.ValueLength = reader.ReadInt32();
        entry.Key = reader.ReadBytes(entry.KeyLength);
        entry.Value = reader.ReadBytes(entry.ValueLength);
        entry.Checksum = reader.ReadUInt32();
        return entry;
    }

    public WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure)
    {
        var result = new WriteAheadLogReadLogEntriesResult<TKey, TValue>
        {
            Success = true
        };
        FileStream.Seek(0, SeekOrigin.Begin);
        var binaryReader = new BinaryReader(FileStream);
        LogEntry entry = default;

        var keyList = new List<TKey>();
        var valuesList = new List<TValue>();
        var i = 0;
        var length = FileStream.Length;
        while (true)
        {
            try
            {
                if (FileStream.Position == length)
                    break;
                ReadLogEntry(binaryReader, ref entry);
            }
            catch (EndOfStreamException e)
            {
                result.Exceptions.Add(i,
                    new EndOfStreamException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                break;
            }
            catch (ObjectDisposedException e)
            {
                result.Exceptions.Add(i, new ObjectDisposedException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                break;
            }
            catch (IOException e)
            {
                result.Exceptions.Add(i, new IOException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                if (stopReadOnException) break;
            }
            catch (Exception e)
            {
                result.Exceptions.Add(i, new InvalidOperationException($"ReadLogEntry failed. Index={i}", e));
                result.Success = false;
                if (stopReadOnException) break;
            }

            if (!entry.ValidateChecksum())
            {
                result.Exceptions.Add(i, new InvalidDataException($"Checksum failed. Index={i}"));
                result.Success = false;
                if (stopReadOnChecksumFailure) break;
            }

            var key = KeySerializer.Deserialize(entry.Key);
            var value = ValueSerializer.Deserialize(entry.Value);
            keyList.Add(key);
            valuesList.Add(value);
            ++i;
        }
        result.Keys = keyList;
        result.Values = valuesList;
        FileStream.Seek(0, SeekOrigin.End);
        return result;
    }

    public void Dispose()
    {
        FileStream.Dispose();
    }

    private void Flush()
    {
        FileStream.Flush();
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

    private void AppendCurrentWalToTheFullLog()
    {
        FileStream.Flush();
        var backupFile = FilePath + ".full";
        var backupDataOffset = sizeof(long) * 3;
        FileStream.Seek(0, SeekOrigin.Begin);
        var existingLength = (int)FileStream.Length;
        var bytes = new byte[existingLength];
        FileStream.Read(bytes, 0, existingLength);
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
        fs.Seek(0, SeekOrigin.End);
        fs.Write(bytes, 0, existingLength);
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
        Flush();
        FileStream.Dispose();
    }
}
