namespace Tenray.ZoneTree.Core;

public struct HeapEntry<TKey, TValue>
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
}