using X86 = System.Runtime.Intrinsics.X86;
using Arm = System.Runtime.Intrinsics.Arm;

namespace Tenray.ZoneTree.WAL;

public sealed class Crc32Computer
{
    public static uint Compute(uint crc, ulong data)
    {
        return ComputeCrc32(crc, data);
    }

    public static uint Compute(uint crc, uint data)
    {
        return ComputeCrc32(crc, data);
    }

    public static uint Compute(uint crc, int data)
    {
        return ComputeCrc32(crc, (uint)data);
    }

    public static uint Compute(uint crc, byte[] array)
    {
        var off = 0;
        var len = array.Length;
        while (len >= 8)
        {
            crc = ComputeCrc32(crc, BitConverter.ToUInt64(array, off));
            off += 8;
            len -= 8;
        }

        while (len > 0)
        {
            crc = ComputeCrc32(crc, array[off]);
            off++;
            len--;
        }
        return crc;
    }

    private static uint ComputeCrc32(uint crc, ulong data)
    {
        if (X86.Sse42.X64.IsSupported)
        {
            return (uint)X86.Sse42.X64.Crc32(crc, data);
        }
        
        if (Arm.Crc32.Arm64.IsSupported)
        {
            return Arm.Crc32.Arm64.ComputeCrc32C(crc, data);
        }

        throw new PlatformNotSupportedException();
    }
    
    private static uint ComputeCrc32(uint crc, uint data)
    {
        if (X86.Sse42.IsSupported)
        {
            return X86.Sse42.Crc32(crc, data);
        }
        
        if (Arm.Crc32.IsSupported)
        {
            return Arm.Crc32.ComputeCrc32C(crc, data);
        }

        throw new PlatformNotSupportedException();
    }
    
    private static uint ComputeCrc32(uint crc, byte data)
    {
        if (X86.Sse42.IsSupported)
        {
            return X86.Sse42.Crc32(crc, data);
        }
        
        if (Arm.Crc32.IsSupported)
        {
            return Arm.Crc32.ComputeCrc32C(crc, data);
        }

        throw new PlatformNotSupportedException();
    }
}