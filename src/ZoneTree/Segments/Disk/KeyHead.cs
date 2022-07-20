using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct KeyHead
{
    public int KeyLength;
    public long KeyOffset;
}
