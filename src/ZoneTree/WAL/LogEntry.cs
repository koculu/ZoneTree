namespace Tenray.ZoneTree.WAL;

public struct LogEntry
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
