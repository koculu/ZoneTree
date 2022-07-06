namespace ZoneTree.Segments.Disk;

public class SparseArrayEntry<TKey, TValue>
{
    public TKey Key;
    
    public TValue Value;
    
    public int Index;

    public SparseArrayEntry(TKey key, TValue value, int index)
    {
        Key = key;
        Value = value;
        Index = index;
    }
}
