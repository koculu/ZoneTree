using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Segments.Model;

[StructLayout(LayoutKind.Sequential)]
public struct KeyHead : IEquatable<KeyHead>
{
    public int KeyLength;

    public long KeyOffset;

    public override bool Equals(object obj)
    {
        return obj is KeyHead head && Equals(head);
    }

    public bool Equals(KeyHead other)
    {
        return KeyLength == other.KeyLength &&
               KeyOffset == other.KeyOffset;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(KeyLength, KeyOffset);
    }

    public static bool operator ==(KeyHead left, KeyHead right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(KeyHead left, KeyHead right)
    {
        return !(left == right);
    }
}
