using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Segments;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public bool ContainsKey(in TKey key)
    {
        TValue value;
        if (SegmentZero.ContainsKey(key))
        {
            if (SegmentZero.TryGet(key, out value))
                return !IsValueDeleted(value);
        }

        foreach (var segment in ReadOnlySegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }

        if (DiskSegment.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }

        foreach (var segment in BottomSegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }
        return false;
    }

    bool TryGetFromReadonlySegments(in TKey key, out TValue value)
    {
        foreach (var segment in ReadOnlySegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }

        if (DiskSegment.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }

        foreach (var segment in BottomSegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }
        return false;
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        if (SegmentZero.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }
        return TryGetFromReadonlySegments(in key, out value);
    }

    public bool TryGetAndUpdate(in TKey key, out TValue value, ValueUpdaterDelegate<TValue> valueUpdater)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

        if (SegmentZero.TryGet(key, out value))
        {
            if (IsValueDeleted(value))
                return false;
        }
        else if (!TryGetFromReadonlySegments(in key, out value))
            return false;

        if (!valueUpdater(ref value))
        {
            // return true because
            // no update happened, but the value is found.
            return true;
        }
        Upsert(in key, in value);
        return true;
    }

    public bool TryAtomicGetAndUpdate(in TKey key, out TValue value, ValueUpdaterDelegate<TValue> valueUpdater)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

        lock (AtomicUpdateLock)
        {
            if (SegmentZero.TryGet(key, out value))
            {
                if (IsValueDeleted(value))
                    return false;
            }
            else if (!TryGetFromReadonlySegments(in key, out value))
                return false;

            if (!valueUpdater(ref value))
            {
                // return true because
                // no update happened, but the value is found.
                return true;
            }
            
            Upsert(in key, in value);
            return true;
        }
    }

    public bool TryAtomicAdd(in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

        lock (AtomicUpdateLock)
        {
            if (ContainsKey(key))
                return false;
            Upsert(key, value);
            return true;
        }
    }

    public bool TryAtomicUpdate(in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

        lock (AtomicUpdateLock)
        {
            if (!ContainsKey(key))
                return false;
            Upsert(key, value);
            return true;
        }
    }

    public bool TryAtomicAddOrUpdate(in TKey key, in TValue valueToAdd, ValueUpdaterDelegate<TValue> valueUpdater)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        AddOrUpdateResult status;
        IMutableSegment<TKey, TValue> segmentZero;
        while (true)
        {
            lock (AtomicUpdateLock)
            {
                segmentZero = SegmentZero;
                if (segmentZero.IsFrozen)
                {
                    status = AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;
                }
                else if (segmentZero.TryGet(in key, out var existing))
                {
                    if (!valueUpdater(ref existing))
                        return false;
                    status = segmentZero.Upsert(key, existing);
                }
                else if (TryGetFromReadonlySegments(in key, out existing))
                {
                    if (!valueUpdater(ref existing))
                        return false;
                    status = segmentZero.Upsert(key, existing);
                }
                else
                {
                    status = segmentZero.Upsert(key, valueToAdd);
                }
            }
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    continue;
                default:
                    return status == AddOrUpdateResult.ADDED;
            }
        }
    }

    public void AtomicUpsert(in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (AtomicUpdateLock)
        {
            Upsert(in key, in value);
        }
    }

    public void Upsert(in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        while (true)
        {
            var segmentZero = SegmentZero;
            var status = segmentZero.Upsert(key, value);
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    continue;
                default:
                    return;
            }
        }
    }

    public bool TryDelete(in TKey key)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        if (!ContainsKey(key))
            return false;
        ForceDelete(in key);
        return true;
    }

    public void ForceDelete(in TKey key)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        while (true)
        {
            var segmentZero = SegmentZero;
            var status = segmentZero.Delete(key);
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    ForceDelete(key);
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    ForceDelete(key);
                    continue;
                default: return;
            }

        }
    }
}
