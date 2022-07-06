using System.Runtime.InteropServices;

namespace ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct ValueHead
{
    public int ValueLength;
    public long ValueOffset;
}
