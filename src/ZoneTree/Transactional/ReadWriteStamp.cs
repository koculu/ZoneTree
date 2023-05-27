using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Transactional;

[StructLayout(LayoutKind.Sequential)]
public struct ReadWriteStamp : IEquatable<ReadWriteStamp>
{
    public long ReadStamp;

    public long WriteStamp;

    public bool IsDeleted => ReadStamp == 0 && WriteStamp == 0;

    public override bool Equals(object obj)
    {
        return obj is ReadWriteStamp stamp && Equals(stamp);
    }

    public bool Equals(ReadWriteStamp other)
    {
        return ReadStamp == other.ReadStamp &&
               WriteStamp == other.WriteStamp &&
               IsDeleted == other.IsDeleted;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ReadStamp, WriteStamp, IsDeleted);
    }

    public static bool operator ==(ReadWriteStamp left, ReadWriteStamp right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ReadWriteStamp left, ReadWriteStamp right)
    {
        return !(left == right);
    }
}
