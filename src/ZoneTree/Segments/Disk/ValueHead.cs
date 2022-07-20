using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct ValueHead
{
    public int ValueLength;
    public long ValueOffset;
}
