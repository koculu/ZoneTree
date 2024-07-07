using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Tenray.ZoneTree.WAL;

public sealed class Crc32Computer_SSE42_X86
{
    public static bool IsSupported => Sse42.IsSupported;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, ulong data)
    {
        crc = (uint)Sse42.Crc32(crc, (uint)data);
        return Sse42.Crc32(crc, (uint)(data >> 32));
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
    public static uint Compute(uint crc, Memory<byte> data)
    {
        var off = 0;
        var len = data.Length;
        while (len >= 4)
        {
            crc = (uint)Sse42.Crc32(crc, Unsafe.ReadUnaligned<uint>(ref data.Span[off]));
            off += 4;
            len -= 4;
        }

        while (len > 0)
        {
            crc = Sse42.Crc32(crc, data.Span[off]);
            off++;
            len--;
        }
        return crc;
    }
}