using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using ZoneTree.Core;
using ZoneTree.WAL;

namespace Tenray.WAL;

// https://devblogs.microsoft.com/dotnet/file-io-improvements-in-dotnet-6/
public sealed class FileSystemWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    readonly FileStream FileStream;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    public string FilePath { get; }

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
        AppendLogEntry(keyBytes, valueBytes);
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
}
