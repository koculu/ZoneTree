using Tenray;
using Tenray.Collections;
using ZoneTree.Collections;
using ZoneTree.Segments.Disk;

namespace ZoneTree.Core;

public class ZoneTreeIterator<TKey, TValue> : IZoneTreeIterator<TKey, TValue>
{
    readonly FixedSizeMinHeap<HeapEntry<TKey, TValue>> Heap;

    readonly IsValueDeletedDelegate<TValue> IsValueDeleted;

    readonly IRefComparer<TKey> Comparer;

    readonly IReadOnlyList<ISeekableIterator<TKey, TValue>> Segments;

    readonly IDiskSegment<TKey, TValue> DiskSegment;

    readonly bool IncludeDeletedRecords;

    readonly int Length;

    bool HasPrev;

    TKey PrevKey = default;
    
    private TKey currentKey;
    
    private TValue currentValue;

    readonly bool IsReverseIterator;

    public TKey CurrentKey {
        get =>
            HasCurrent ?
            currentKey :
            throw new ZoneTreeIteratorPositionException(); 
        private set => currentKey = value;
    }

    public TValue CurrentValue { 
        get => 
            HasCurrent ?
            currentValue :
            throw new ZoneTreeIteratorPositionException();
        private set => currentValue = value; 
    }

    public bool HasCurrent { get; private set; }

    public KeyValuePair<TKey, TValue> Current => KeyValuePair.Create(CurrentKey, CurrentValue);

    public ZoneTreeIterator(
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<ISeekableIterator<TKey, TValue>> segments,
        IRefComparer<HeapEntry<TKey, TValue>> heapEntryComparer,
        IDiskSegment<TKey, TValue> diskSegment,
        bool isReverseIterator,
        bool includeDeletedRecords)
    {
        Heap = new FixedSizeMinHeap<HeapEntry<TKey, TValue>>
            (segments.Count + 1, heapEntryComparer);
        IsValueDeleted = options.IsValueDeleted;
        Comparer = options.Comparer;
        Segments = segments;
        DiskSegment = diskSegment;
        IsReverseIterator = isReverseIterator;
        IncludeDeletedRecords = includeDeletedRecords;
        Length = segments.Count;
        FillHeap();
    }

    public bool Next()
    {
        return IsReverseIterator ? PrevInternal() : NextInternal();
    }

    public void Seek(in TKey key)
    {
        Heap.Clear();
        PrevKey = default;
        HasPrev = false;
        CurrentKey = default;
        CurrentValue = default;
        HasCurrent = false;

        var len = Length;
        if (IsReverseIterator)
        {
            for (int i = 0; i < len; i++)
            {
                var s = Segments[i];
                if (!s.SeekToLastSmallerOrEqualElement(in key))
                    continue;
                var entry = new HeapEntry<TKey, TValue>(s.CurrentKey, s.CurrentValue, i);
                Heap.Insert(entry);
            }
            return;
        }
        for (int i = 0; i < len; i++)
        {
            var s = Segments[i];
            if (!s.SeekToFirstGreaterOrEqualElement(in key))
                continue;
            var entry = new HeapEntry<TKey, TValue>(s.CurrentKey, s.CurrentValue, i);
            Heap.Insert(entry);
        }
    }

    public void Reset()
    {
        Heap.Clear();
        PrevKey = default;
        HasPrev = false;
        CurrentKey = default;
        CurrentValue = default;
        HasCurrent = false;
        FillHeap();
    }

    public void Dispose()
    {
        DiskSegment?.RemoveReader();
    }

    void FillHeap()
    {
        var len = Length;
        if (IsReverseIterator)
        {
            for (int i = 0; i < len; i++)
            {
                var s = Segments[i];
                if (!s.SeekEnd())
                    continue;
                var key = s.CurrentKey;
                var value = s.CurrentValue;
                var entry = new HeapEntry<TKey, TValue>(key, value, i);
                Heap.Insert(entry);
            }
            return;
        }
        for (int i = 0; i < len; i++)
        {
            var s = Segments[i];
            if (!s.SeekBegin())
                continue;
            var key = s.CurrentKey;
            var value = s.CurrentValue;
            var entry = new HeapEntry<TKey, TValue>(key, value, i);
            Heap.Insert(entry);
        }
    }

    bool PrevInternal()
    {
        int minSegmentIndex = 0;
        var skipElement = () =>
        {
            var minSegment = Segments[minSegmentIndex];
            if (minSegment.Prev())
            {
                var key = minSegment.CurrentKey;
                var value = minSegment.CurrentValue;
                Heap.ReplaceMin(new HeapEntry<TKey, TValue>(key, value, minSegmentIndex));
            }
            else
            {
                Heap.RemoveMin();
            }
        };

        while (Heap.Count > 0)
        {
            var minEntry = Heap.MinValue;
            minSegmentIndex = minEntry.SegmentIndex;

            // ignore deleted entries.
            if (!IncludeDeletedRecords && IsValueDeleted(minEntry.Value))
            {
                skipElement();
                PrevKey = minEntry.Key;
                HasPrev = true;
                continue;
            }

            if (HasPrev && Comparer.Compare(minEntry.Key, PrevKey) == 0)
            {
                skipElement();
                continue;
            }
            PrevKey = minEntry.Key;
            HasPrev = true;

            CurrentKey = minEntry.Key;
            CurrentValue = minEntry.Value;
            HasCurrent = true;
            skipElement();
            return true;
        }
        HasCurrent = false;
        return false;
    }

    bool NextInternal()
    {
        int minSegmentIndex = 0;
        var skipElement = () =>
        {
            var minSegment = Segments[minSegmentIndex];
            if (minSegment.Next())
            {
                var key = minSegment.CurrentKey;
                var value = minSegment.CurrentValue;
                Heap.ReplaceMin(new HeapEntry<TKey, TValue>(key, value, minSegmentIndex));
            }
            else
            {
                Heap.RemoveMin();
            }
        };

        while (Heap.Count > 0)
        {
            var minEntry = Heap.MinValue;
            minSegmentIndex = minEntry.SegmentIndex;

            // ignore deleted entries.
            if (!IncludeDeletedRecords && IsValueDeleted(minEntry.Value))
            {
                skipElement();
                PrevKey = minEntry.Key;
                HasPrev = true;
                continue;
            }

            if (HasPrev && Comparer.Compare(minEntry.Key, PrevKey) == 0)
            {
                skipElement();
                continue;
            }
            PrevKey = minEntry.Key;
            HasPrev = true;

            CurrentKey = minEntry.Key;
            CurrentValue = minEntry.Value;
            HasCurrent = true;
            skipElement();
            return true;
        }
        HasCurrent = false;
        return false;
    }
}
