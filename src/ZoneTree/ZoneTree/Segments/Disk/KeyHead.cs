using System.Runtime.InteropServices;

namespace ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct KeyHead
{
    public int KeyLength;
    public long KeyOffset;
}
