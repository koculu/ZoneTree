using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Segments.Disk;

[StructLayout(LayoutKind.Sequential)]
public struct EntryHead : IEquatable<EntryHead>
{
    public int KeyLength;

    public long KeyOffset;

    public int ValueLength;

    public long ValueOffset;

    public override bool Equals(object obj)
    {
        return obj is EntryHead head && Equals(head);
    }

    public bool Equals(EntryHead other)
    {
        return KeyLength == other.KeyLength &&
               KeyOffset == other.KeyOffset &&
               ValueLength == other.ValueLength &&
               ValueOffset == other.ValueOffset;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(KeyLength, KeyOffset, ValueLength, ValueOffset);
    }

    public static bool operator ==(EntryHead left, EntryHead right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EntryHead left, EntryHead right)
    {
        return !(left == right);
    }
}
