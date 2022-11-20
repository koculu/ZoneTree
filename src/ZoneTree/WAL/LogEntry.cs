using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.WAL;

public struct LogEntry
{
    public long OpIndex;
    public int KeyLength;
    public int ValueLength;
    public byte[] Key;
    public byte[] Value;
    public uint Checksum;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateChecksum()
    {
        return CreateChecksum() == Checksum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendLogEntry(
        BinaryWriter binaryWriter,
        byte[] keyBytes,
        byte[] valueBytes,
        long opIndex)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadLogEntry(BinaryReader reader, ref LogEntry entry)
    {
        entry.OpIndex = reader.ReadInt64();
        entry.KeyLength = reader.ReadInt32();
        entry.ValueLength = reader.ReadInt32();
        entry.Key = reader.ReadBytes(entry.KeyLength);
        entry.Value = reader.ReadBytes(entry.ValueLength);
        entry.Checksum = reader.ReadUInt32();
    }
}
