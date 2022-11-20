using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;

namespace Tenray.ZoneTree.WAL;

public sealed class Crc32Computer_ARM64
{
    public static bool IsSupported => Crc32.Arm64.IsSupported;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, ulong data)
    {
        return Crc32.Arm64.ComputeCrc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, uint data)
    {
        return Crc32.ComputeCrc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, int data)
    {
        return Crc32.ComputeCrc32(crc, (uint)data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, byte[] data)
    {
        var off = 0;
        var len = data.Length;
        while (len >= 8)
        {
            crc = Crc32.Arm64.ComputeCrc32(crc, BitConverter.ToUInt64(data, off));
            off += 8;
            len -= 8;
        }

        while (len > 0)
        {
            crc = Crc32.Arm64.ComputeCrc32(crc, data[off]);
            off++;
            len--;
        }
        return crc;
    }
}