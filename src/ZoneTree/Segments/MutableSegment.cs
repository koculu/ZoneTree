using Tenray.Collections;
using Tenray.WAL;
using ZoneTree.Collections;
using ZoneTree.Core;

namespace Tenray.Segments;

public class MutableSegment<TKey, TValue> : IMutableSegment<TKey, TValue>
{
    readonly ZoneTreeOptions<TKey, TValue> Options;

    volatile bool IsFrozenFlag = false;

    int WritesInProgress = 0;
    
    readonly MarkValueDeletedDelegate<TValue> MarkValueDeleted;

    readonly int MutableSegmentMaxItemCount;

    readonly SkipList<TKey, TValue> SkipList;

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public int SegmentId { get; private set; }

    public bool IsFrozen => IsFrozenFlag;

    public bool IsFullyFrozen => IsFrozenFlag && WritesInProgress == 0;

    public int Length => SkipList.Length;

    public MutableSegment(ZoneTreeOptions<TKey, TValue> options,
        int segmentId)
    {
        SegmentId = segmentId;
        WriteAheadLog = options.WriteAheadLogProvider.GetOrCreateWAL(SegmentId);
        Options = options;
        Comparer = options.Comparer;
        SkipList = new(Comparer, (int)Math.Log2(options.MutableSegmentMaxItemCount) + 1);
        MarkValueDeleted = options.MarkValueDeleted;
        MutableSegmentMaxItemCount = options.MutableSegmentMaxItemCount;
    }

    public MutableSegment(
        int segmentId,
        IWriteAheadLog<TKey, TValue> wal, 
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<TKey> keys,
        IReadOnlyList<TValue> values)
    {
        SegmentId = segmentId;
        WriteAheadLog = wal;
        Options = options;
        Comparer = options.Comparer;
        SkipList = new(Comparer, (int)Math.Log2(options.MutableSegmentMaxItemCount) + 1);
        MarkValueDeleted = options.MarkValueDeleted;
        MutableSegmentMaxItemCount = options.MutableSegmentMaxItemCount;
        LoadLogEntries(keys, values);
    }

    private void LoadLogEntries(IReadOnlyList<TKey> keys, IReadOnlyList<TValue> values)
    {
        var len = keys.Count;
        for (var i = 0; i < len; ++i)
        {
            var key = keys[i];
            var value = values[i];
            // TODO: Search if we can create faster construction
            // of mutable segment from log entries.
            SkipList.AddOrUpdate(key,
                (x) =>
                {
                    x.Value = value;
                    return AddOrUpdateResult.ADDED;
                },
                (y) =>
                {
                    y.Value = value;
                    return AddOrUpdateResult.UPDATED;
                });
        }
    }

    public bool ContainsKey(in TKey key)
    {
        return SkipList.ContainsKey(key);
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        return SkipList.TryGetValue(key, out value);
    }

    public AddOrUpdateResult Upsert(TKey key, TValue value)
    {
        if (IsFrozenFlag)
            return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (SkipList.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;

            var status = SkipList.AddOrUpdate(key,
                (x) =>
                {
                    WriteAheadLog.Append(in key, in value);
                    x.Value = value;
                    return AddOrUpdateResult.ADDED;
                },
                (x) =>
                {
                    WriteAheadLog.Append(in key, in value);
                    x.Value = value;
                    return AddOrUpdateResult.UPDATED;
                });
            return status;
        }
        finally
        {
            Interlocked.Decrement(ref WritesInProgress);
        }
    }

    public AddOrUpdateResult Delete(TKey key)
    {
        if (IsFrozenFlag)
            return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (SkipList.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;

            var status = SkipList.AddOrUpdate(key,
                (x) =>
                {
                    ref var value = ref x.GetValueRef();
                    MarkValueDeleted(ref value);
                    WriteAheadLog.Append(in key, in value);
                    return AddOrUpdateResult.ADDED;
                },
                (x) =>
                {
                    ref var value = ref x.GetValueRef();
                    MarkValueDeleted(ref value);
                    WriteAheadLog.Append(in key, in value);
                    return AddOrUpdateResult.UPDATED;
                });
            return status;
        }
        finally
        {
            Interlocked.Decrement(ref WritesInProgress);
        }
    }

    public IReadOnlySegment<TKey, TValue> CreateReadOnlySegment()
    {
        if (!IsFullyFrozen)
            throw new Exception("Freeze the segment zero first!");
                
        var (keys, values) = SkipList.ToArray();

        var readOnlySegment =
            new ReadOnlySegment<TKey, TValue>(SegmentId, Options, keys, values);
        return readOnlySegment;
    }

    public void Freeze()
    {
        IsFrozenFlag = true;
    }

    public void Drop()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId);
        WriteAheadLog?.Drop();
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return new SkipListIndexedReader<TKey, TValue>(SkipList);
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SkipListSeekableIterator<TKey, TValue>(SkipList);
    }

    public void ReleaseResources()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId);
        WriteAheadLog?.Dispose();
    }
}
