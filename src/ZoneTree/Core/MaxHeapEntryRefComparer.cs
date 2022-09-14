using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Core;

public sealed class MaxHeapEntryRefComparer<TKey, TValue> : IRefComparer<HeapEntry<TKey, TValue>>
{
    public IRefComparer<TKey> KeyComparer { get; }

    public MaxHeapEntryRefComparer(IRefComparer<TKey> keyComparer)
    {
        KeyComparer = keyComparer;
    }

    public int Compare(in HeapEntry<TKey, TValue> x, in HeapEntry<TKey, TValue> y)
    {
        var result = KeyComparer.Compare(y.Key, x.Key);
        if (result == 0)
            return x.SegmentIndex - y.SegmentIndex;
        return result;
    }
}