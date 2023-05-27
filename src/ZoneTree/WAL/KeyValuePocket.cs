namespace Tenray.ZoneTree.WAL;

public struct KeyValuePocket<TKey, TValue> : IEquatable<KeyValuePocket<TKey, TValue>>
{
    public TKey Key;
    public TValue Value;

    public KeyValuePocket(TKey key, TValue value) : this()
    {
        Key = key;
        Value = value;
    }

    public override bool Equals(object obj)
    {
        return obj is KeyValuePocket<TKey, TValue> pocket && Equals(pocket);
    }

    public bool Equals(KeyValuePocket<TKey, TValue> other)
    {
        return EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
               EqualityComparer<TValue>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value);
    }

    public static bool operator ==(KeyValuePocket<TKey, TValue> left, KeyValuePocket<TKey, TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(KeyValuePocket<TKey, TValue> left, KeyValuePocket<TKey, TValue> right)
    {
        return !(left == right);
    }
}

