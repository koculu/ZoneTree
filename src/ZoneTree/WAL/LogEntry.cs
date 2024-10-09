using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.WAL;

public struct LogEntry : IEquatable<LogEntry>
{
    public long OpIndex;

    public int KeyLength;

    public int ValueLength;

    public Memory<byte> Key;

    public Memory<byte> Value;

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
            crc32 = Crc32Computer_SSE42_X64.Compute(crc32, Key.Span);
            crc32 = Crc32Computer_SSE42_X64.Compute(crc32, Value.Span);
            return crc32;
        }

        if (Crc32Computer_SSE42_X86.IsSupported)
        {
            crc32 = Crc32Computer_SSE42_X86.Compute(crc32, (ulong)OpIndex);
            crc32 = Crc32Computer_SSE42_X86.Compute(crc32, KeyLength);
            crc32 = Crc32Computer_SSE42_X86.Compute(crc32, ValueLength);
            crc32 = Crc32Computer_SSE42_X86.Compute(crc32, Key);
            crc32 = Crc32Computer_SSE42_X86.Compute(crc32, Value);
            return crc32;
        }

        if (Crc32Computer_ARM64.IsSupported)
        {
            crc32 = Crc32Computer_ARM64.Compute(crc32, (ulong)OpIndex);
            crc32 = Crc32Computer_ARM64.Compute(crc32, KeyLength);
            crc32 = Crc32Computer_ARM64.Compute(crc32, ValueLength);
            crc32 = Crc32Computer_ARM64.Compute(crc32, Key.Span);
            crc32 = Crc32Computer_ARM64.Compute(crc32, Value.Span);
            return crc32;
        }

        crc32 = Crc32Computer_Software.Compute(crc32, (ulong)OpIndex);
        crc32 = Crc32Computer_Software.Compute(crc32, KeyLength);
        crc32 = Crc32Computer_Software.Compute(crc32, ValueLength);
        crc32 = Crc32Computer_Software.Compute(crc32, Key.Span);
        crc32 = Crc32Computer_Software.Compute(crc32, Value.Span);
        return crc32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateChecksum()
    {
        return CreateChecksum() == Checksum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendLogEntry(
        BinaryWriter binaryWriter,
        Memory<byte> keyBytes,
        Memory<byte> valueBytes,
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
        if (!entry.Key.IsEmpty)
            binaryWriter.Write(entry.Key.Span);
        if (!entry.Value.IsEmpty)
            binaryWriter.Write(entry.Value.Span);
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

    public override bool Equals(object obj)
    {
        return obj is LogEntry entry && Equals(entry);
    }

    public bool Equals(LogEntry other)
    {
        return OpIndex == other.OpIndex &&
               KeyLength == other.KeyLength &&
               ValueLength == other.ValueLength &&
               Key.Span.SequenceEqual(other.Key.Span) &&
               Value.Span.SequenceEqual(other.Value.Span) &&
               Checksum == other.Checksum;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(OpIndex, KeyLength, ValueLength, Key, Value, Checksum);
    }

    public static bool operator ==(LogEntry left, LogEntry right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(LogEntry left, LogEntry right)
    {
        return !(left == right);
    }
}
