using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.PresetTypes;

[StructLayout(LayoutKind.Sequential)]
public struct Deletable<TValue> : IEquatable<Deletable<TValue>>
{
    public TValue Value;

    public bool IsDeleted;

    public Deletable(in TValue value, bool isDeleted = false) : this()
    {
        Value = value;
        IsDeleted = isDeleted;
    }

    public override bool Equals(object obj)
    {
        return obj is Deletable<TValue> deletable && Equals(deletable);
    }

    public bool Equals(Deletable<TValue> other)
    {
        return EqualityComparer<TValue>.Default.Equals(Value, other.Value) &&
               IsDeleted == other.IsDeleted;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, IsDeleted);
    }

    public static bool operator ==(Deletable<TValue> left, Deletable<TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Deletable<TValue> left, Deletable<TValue> right)
    {
        return !(left == right);
    }
}
