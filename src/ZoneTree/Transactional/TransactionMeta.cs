using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Transactional;

[StructLayout(LayoutKind.Sequential)]
public struct TransactionMeta : IEquatable<TransactionMeta>
{
    public TransactionState State;

    public long StartedAt;

    public long EndedAt;

    public override bool Equals(object obj)
    {
        return obj is TransactionMeta meta && Equals(meta);
    }

    public bool Equals(TransactionMeta other)
    {
        return State == other.State &&
               StartedAt == other.StartedAt &&
               EndedAt == other.EndedAt;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(State, StartedAt, EndedAt);
    }

    public static bool operator ==(TransactionMeta left, TransactionMeta right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TransactionMeta left, TransactionMeta right)
    {
        return !(left == right);
    }
}
