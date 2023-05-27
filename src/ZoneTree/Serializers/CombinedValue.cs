using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Serializers;

[StructLayout(LayoutKind.Sequential)]
public struct CombinedValue<TValue1, TValue2> : IEquatable<CombinedValue<TValue1, TValue2>>
{
    public TValue1 Value1;

    public TValue2 Value2;

    public CombinedValue(in TValue1 value1, in TValue2 value2) : this()
    {
        Value1 = value1;
        Value2 = value2;
    }

    public override bool Equals(object obj)
    {
        return obj is CombinedValue<TValue1, TValue2> value && Equals(value);
    }

    public bool Equals(CombinedValue<TValue1, TValue2> other)
    {
        return EqualityComparer<TValue1>.Default.Equals(Value1, other.Value1) &&
               EqualityComparer<TValue2>.Default.Equals(Value2, other.Value2);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value1, Value2);
    }

    public static bool operator ==(CombinedValue<TValue1, TValue2> left, CombinedValue<TValue1, TValue2> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CombinedValue<TValue1, TValue2> left, CombinedValue<TValue1, TValue2> right)
    {
        return !(left == right);
    }
}
