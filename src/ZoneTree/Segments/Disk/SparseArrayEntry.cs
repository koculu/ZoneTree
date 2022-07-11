namespace ZoneTree.Segments.Disk;

public class SparseArrayEntry<TKey, TValue>
{
    public readonly TKey Key;
    
    public readonly TValue Value;
    
    public readonly int Index;

    public SparseArrayEntry(TKey key, TValue value, int index)
    {
        Key = key;
        Value = value;
        Index = index;
    }
}
