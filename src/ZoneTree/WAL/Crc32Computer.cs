using System.Runtime.Intrinsics.X86;

namespace ZoneTree.WAL;

public class Crc32Computer
{
    public static uint Compute(uint crc, ulong data)
    {
        return (uint)Sse42.X64.Crc32(crc, data);
    }

    public static uint Compute(uint crc, uint data)
    {
        return Sse42.Crc32(crc, data);
    }

    public static uint Compute(uint crc, int data)
    {
        return Sse42.Crc32(crc, (uint)data);
    }

    public static uint Compute(uint crc, byte[] array)
    {
        var off = 0;
        var len = array.Length;
        while (len >= 8)
        {
            crc = (uint)Sse42.X64.Crc32(crc, BitConverter.ToUInt64(array, off));
            off += 8;
            len -= 8;
        }

        while (len > 0)
        {
            crc = Sse42.Crc32(crc, array[off]);
            off++;
            len--;
        }
        return crc;
    }
}