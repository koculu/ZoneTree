using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct ValueHead : IEquatable<ValueHead>
{
    public int ValueLength;

    public long ValueOffset;

    public override bool Equals(object obj)
    {
        return obj is ValueHead head && Equals(head);
    }

    public bool Equals(ValueHead other)
    {
        return ValueLength == other.ValueLength &&
               ValueOffset == other.ValueOffset;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ValueLength, ValueOffset);
    }

    public static bool operator ==(ValueHead left, ValueHead right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ValueHead left, ValueHead right)
    {
        return !(left == right);
    }
}
