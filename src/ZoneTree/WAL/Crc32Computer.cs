using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace Tenray.ZoneTree.WAL;

public sealed class Crc32Computer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, ulong data)
    {
        return ComputeCrc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, uint data)
    {
        return ComputeCrc32(crc, data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, int data)
    {
        return ComputeCrc32(crc, (uint)data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Compute(uint crc, byte[] data)
    {
        if (Sse42.X64.IsSupported)
            return ComputeX64(crc, data);

        if (Crc32.Arm64.IsSupported)
            return ComputeARM(crc, data);

        throw new PlatformNotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ComputeX64(uint crc, byte[] data)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ComputeARM(uint crc, byte[] data)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ComputeCrc32(uint crc, ulong data)
    {
        if (Sse42.X64.IsSupported)
        {
            return (uint)Sse42.X64.Crc32(crc, data);
        }

        if (Crc32.Arm64.IsSupported)
        {
            return Crc32.Arm64.ComputeCrc32C(crc, data);
        }

        throw new PlatformNotSupportedException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint ComputeCrc32(uint crc, uint data)
    {
        if (Sse42.IsSupported)
        {
            return Sse42.Crc32(crc, data);
        }

        if (Crc32.IsSupported)
        {
            return Crc32.ComputeCrc32C(crc, data);
        }

        throw new PlatformNotSupportedException();
    }
}