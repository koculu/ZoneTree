namespace Tenray.ZoneTree.Segments.Disk;

public sealed class SparseArrayEntry<TKey, TValue>
{
    public readonly TKey Key;

    public readonly TValue Value;

    public readonly long Index;

    public SparseArrayEntry(TKey key, TValue value, long index)
    {
        Key = key;
        Value = value;
        Index = index;
    }
}
