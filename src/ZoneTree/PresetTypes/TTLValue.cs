using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.PresetTypes;

[StructLayout(LayoutKind.Sequential)]
public struct TTLValue<TValue> : IEquatable<TTLValue<TValue>>
{
    public TValue Value;

    public DateTime Expiration;

    public TTLValue(in TValue value, DateTime expiration)
    {
        Value = value;
        Expiration = expiration;
    }

    public bool IsExpired => DateTime.UtcNow >= Expiration;

    public override bool Equals(object obj)
    {
        return obj is TTLValue<TValue> value && Equals(value);
    }

    public bool Equals(TTLValue<TValue> other)
    {
        return EqualityComparer<TValue>.Default.Equals(Value, other.Value) &&
               Expiration == other.Expiration &&
               IsExpired == other.IsExpired;
    }

    public void Expire()
    {
        Expiration = new DateTime();
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, Expiration, IsExpired);
    }

    public bool SlideExpiration(TimeSpan timeSpan)
    {
        var newExpiration = DateTime.UtcNow.Add(timeSpan);
        if (newExpiration <= Expiration)
            return false;
        Expiration = newExpiration;
        return true;
    }

    public static bool operator ==(TTLValue<TValue> left, TTLValue<TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TTLValue<TValue> left, TTLValue<TValue> right)
    {
        return !(left == right);
    }
}
