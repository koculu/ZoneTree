﻿using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTreeIterator<TKey, TValue> : IZoneTreeIterator<TKey, TValue>
{
    readonly ZoneTree<TKey, TValue> ZoneTree;

    readonly IRefComparer<HeapEntry<TKey, TValue>> HeapEntryComparer;

    readonly bool IsReverseIterator;

    FixedSizeMinHeap<HeapEntry<TKey, TValue>> Heap;

    IReadOnlyList<ISeekableIterator<TKey, TValue>> SeekableIterators;

    readonly IsValueDeletedDelegate<TValue> IsValueDeleted;

    readonly IRefComparer<TKey> Comparer;

    readonly bool IncludeDeletedRecords;

    readonly bool IncludeMutableSegment;

    readonly bool IncludeDiskSegment;

    readonly bool IncludeBottomSegments;

    int Length;

    bool HasPrev;

    TKey PrevKey = default;

    TKey currentKey;

    TValue currentValue;

    bool IsHeapFilled;

    volatile bool DoesRequireRefresh;

    public TKey CurrentKey
    {
        get =>
            HasCurrent ?
            currentKey :
            throw new ZoneTreeIteratorPositionException();
        private set => currentKey = value;
    }

    public TValue CurrentValue
    {
        get =>
            HasCurrent ?
            currentValue :
            throw new ZoneTreeIteratorPositionException();
        private set => currentValue = value;
    }

    public bool HasCurrent { get; private set; }

    public bool AutoRefresh { get; }

    public KeyValuePair<TKey, TValue> Current => KeyValuePair.Create(CurrentKey, CurrentValue);

    public IDiskSegment<TKey, TValue> DiskSegment { get; private set; }

    public IReadOnlyList<IDiskSegment<TKey, TValue>> BottomSegments { get; private set; }

    public ZoneTreeIterator(
        ZoneTreeOptions<TKey, TValue> options,
        ZoneTree<TKey, TValue> zoneTree,
        IRefComparer<HeapEntry<TKey, TValue>> heapEntryComparer,
        bool autoRefresh,
        bool isReverseIterator,
        bool includeDeletedRecords,
        bool includeMutableSegment,
        bool includeDiskSegment,
        bool includeBottomSegments)
    {
        IsValueDeleted = options.IsValueDeleted;
        Comparer = options.Comparer;
        ZoneTree = zoneTree;
        HeapEntryComparer = heapEntryComparer;
        AutoRefresh = autoRefresh;
        IsReverseIterator = isReverseIterator;
        IncludeDeletedRecords = includeDeletedRecords;
        IncludeMutableSegment = includeMutableSegment;
        IncludeDiskSegment = includeDiskSegment;
        if (autoRefresh)
            ZoneTree.OnMutableSegmentMovedForward += OnZoneTreeMutableSegmentMovedForward;
        ZoneTree.OnZoneTreeIsDisposing += OnZoneTreeIsDisposing;
        IncludeBottomSegments = includeBottomSegments;
    }

    void OnZoneTreeIsDisposing(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        ReleaseResources();
    }

    void OnZoneTreeMutableSegmentMovedForward(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        DoesRequireRefresh = true;
    }

    public bool Next()
    {
        if (DoesRequireRefresh)
            Refresh();
        if (!IsHeapFilled)
            SeekFirst();
        return IsReverseIterator ? PrevInternal() : NextInternal();
    }

    public void Seek(in TKey key)
    {
        if (DoesRequireRefresh || Heap == null)
        {
            ClearMarkers();
            Refresh();
        }
        SeekInternal(in key, true);
    }

    public void SeekFirst()
    {
        ClearMarkers();
        if (Heap == null)
        {
            Refresh();
        }
        Heap.Clear();
        IsHeapFilled = false;
        var len = Length;
        if (IsReverseIterator)
        {
            for (int i = 0; i < len; i++)
            {
                var s = SeekableIterators[i];
                if (!s.SeekEnd())
                    continue;
                var key = s.CurrentKey;
                var value = s.CurrentValue;
                var entry = new HeapEntry<TKey, TValue>(key, value, i);
                Heap.Insert(entry);
            }
            IsHeapFilled = true;
            return;
        }
        for (int i = 0; i < len; i++)
        {
            var s = SeekableIterators[i];
            if (!s.SeekBegin())
                continue;
            var key = s.CurrentKey;
            var value = s.CurrentValue;
            var entry = new HeapEntry<TKey, TValue>(key, value, i);
            Heap.Insert(entry);
        }
        IsHeapFilled = true;
    }

    void SeekInternal(in TKey key, bool clearMarkers)
    {
        Heap.Clear();
        IsHeapFilled = false;
        if (clearMarkers)
            ClearMarkers();

        var len = Length;
        if (IsReverseIterator)
        {
            for (int i = 0; i < len; i++)
            {
                var s = SeekableIterators[i];
                if (!s.SeekToLastSmallerOrEqualElement(in key))
                    continue;
                var entry = new HeapEntry<TKey, TValue>(s.CurrentKey, s.CurrentValue, i);
                Heap.Insert(entry);
            }
            IsHeapFilled = true;
            return;
        }
        for (int i = 0; i < len; i++)
        {
            var s = SeekableIterators[i];
            if (!s.SeekToFirstGreaterOrEqualElement(in key))
                continue;
            var entry = new HeapEntry<TKey, TValue>(s.CurrentKey, s.CurrentValue, i);
            Heap.Insert(entry);
        }
        IsHeapFilled = true;
    }

    void ReleaseResources()
    {
        IsHeapFilled = false;
        Heap = null;
        DetachFromDiskSegments();
        SeekableIterators = null;
    }

    void DetachFromDiskSegments()
    {
        lock (this)
        {
            DiskSegment?.DetachIterator();
            DiskSegment = null;
            if (BottomSegments == null)
                return;
            var bos = BottomSegments;
            var len = bos.Count;
            for (var i = 0; i < len; ++i)
            {
                bos[i]?.DetachIterator();
            }
            BottomSegments = null;
        }
    }

    void ClearMarkers()
    {
        PrevKey = default;
        HasPrev = false;
        CurrentKey = default;
        CurrentValue = default;
        HasCurrent = false;
    }

    public void Refresh()
    {
        DoesRequireRefresh = false;
        IsHeapFilled = false;
        ReleaseResources();
        var segments = ZoneTree
            .CollectSegments(
                IncludeMutableSegment,
                IncludeDiskSegment,
                IncludeBottomSegments);
        DiskSegment = segments.DiskSegment;
        BottomSegments = segments.BottomSegments;
        SeekableIterators = segments.SeekableIterators;
        Length = SeekableIterators.Count;
        Heap = new FixedSizeMinHeap<HeapEntry<TKey, TValue>>(Length + 1, HeapEntryComparer);
        if (HasCurrent)
        {
            SeekInternal(in currentKey, false);
        }
    }

    bool PrevInternal()
    {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        int minSegmentIndex = 0;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
        var skipElement = () =>
        {
            var minSegment = SeekableIterators[minSegmentIndex];
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
#pragma warning disable IDE0059 // Unnecessary assignment of a value
        int minSegmentIndex = 0;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
        var skipElement = () =>
        {
            var minSegment = SeekableIterators[minSegmentIndex];
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

    public void Dispose()
    {
        if (AutoRefresh)
            ZoneTree.OnMutableSegmentMovedForward -= OnZoneTreeMutableSegmentMovedForward;
        ZoneTree.OnZoneTreeIsDisposing -= OnZoneTreeIsDisposing;
        ReleaseResources();
    }

    public void WaitUntilReadOnlySegmentsBecomeFullyFrozen()
    {
        var iterators = SeekableIterators;
        var spinWait = new SpinWait();
        while (true)
        {
            if (iterators.Any(x => !x.IsFullyFrozen))
            {
                spinWait.SpinOnce();
                continue;
            }
            return;
        }
    }
}
