using System.Buffers;

namespace Tenray.ZoneTree.AbstractFileStream;

public static class MemoryStreamExtensions
{
    public static unsafe Stream ToReadOnlyStream(this Memory<byte> memory, MemoryHandle pin)
    {
        return new UnmanagedMemoryStream(
                (byte*)pin.Pointer,
                memory.Length, memory.Length, FileAccess.Read);
    }
}