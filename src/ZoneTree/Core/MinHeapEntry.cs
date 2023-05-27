using System.Drawing;

namespace Tenray.ZoneTree.Core;

public struct HeapEntry<TKey, TValue> : IEquatable<HeapEntry<TKey, TValue>>
{
    public TKey Key;

    public TValue Value;

    public int SegmentIndex;

    public HeapEntry(TKey key, TValue value, int segmentIndex)
    {
        Key = key;
        Value = value;
        SegmentIndex = segmentIndex;
    }

    public override bool Equals(object obj)
    {
        return obj is HeapEntry<TKey, TValue> entry && Equals(entry);
    }

    public bool Equals(HeapEntry<TKey, TValue> other)
    {
        return EqualityComparer<TKey>.Default.Equals(Key, other.Key) &&
               EqualityComparer<TValue>.Default.Equals(Value, other.Value) &&
               SegmentIndex == other.SegmentIndex;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value, SegmentIndex);
    }

    public static bool operator ==(HeapEntry<TKey, TValue> left, HeapEntry<TKey, TValue> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HeapEntry<TKey, TValue> left, HeapEntry<TKey, TValue> right)
    {
        return !(left == right);
    }
}