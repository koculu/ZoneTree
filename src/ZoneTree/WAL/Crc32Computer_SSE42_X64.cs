using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Tenray.ZoneTree.WAL;

public sealed class Crc32Computer_SSE42_X64
{
    public static bool IsSupported => Sse42.X64.IsSupported;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, ulong data)
    {
        return (uint)Sse42.X64.Crc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, uint data)
    {
        return Sse42.Crc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, int data)
    {
        return Sse42.Crc32(crc, (uint)data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, byte[] data)
    {
        var off = 0;
        var len = data.Length;
        while (len >= 8)
        {
            crc = (uint)Sse42.X64.Crc32(crc, BitConverter.ToUInt64(data, off));
            off += 8;
            len -= 8;
        }

        while (len > 0)
        {
            crc = Sse42.Crc32(crc, data[off]);
            off++;
            len--;
        }
        return crc;
    }
}