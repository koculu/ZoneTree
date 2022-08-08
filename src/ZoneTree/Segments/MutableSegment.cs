#undef USE_LOCK_FREE_SKIP_LIST

/*
 * Lock Free Skip List is turned off by default.
 * Because it is slower in most of the test cases.
 * The option is pinned here for future analysis and improvements
 * on lock-free skiplist implementation.
 * It might have advantages when the multi-threaded updates/inserts occur 
 * in different regions in the list.
 */

using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments;

public class MutableSegment<TKey, TValue> : IMutableSegment<TKey, TValue>
{
    readonly ZoneTreeOptions<TKey, TValue> Options;

    volatile bool IsFrozenFlag = false;

    volatile int WritesInProgress = 0;

    readonly MarkValueDeletedDelegate<TValue> MarkValueDeleted;

    readonly int MutableSegmentMaxItemCount;

#if USE_LOCK_FREE_SKIP_LIST
    readonly LockFreeSkipList<TKey, TValue> SkipList;
#else
    readonly SkipList<TKey, TValue> SkipList;
#endif

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public long SegmentId { get; private set; }

    public bool IsFrozen => IsFrozenFlag;

    public bool IsFullyFrozen => IsFrozenFlag && WritesInProgress == 0;

    public int Length => SkipList.Length;

    public MutableSegment(ZoneTreeOptions<TKey, TValue> options,
        long segmentId)
    {
        SegmentId = segmentId;
        WriteAheadLog = options.WriteAheadLogProvider
            .GetOrCreateWAL(
                SegmentId,
                ZoneTree<TKey, TValue>.SegmentWalCategory,
                options.KeySerializer,
                options.ValueSerializer);
        Options = options;
        Comparer = options.Comparer;
        SkipList = new(Comparer, (int)Math.Log2(options.MutableSegmentMaxItemCount) + 1);
        MarkValueDeleted = options.MarkValueDeleted;
        MutableSegmentMaxItemCount = options.MutableSegmentMaxItemCount;
    }

    public MutableSegment(
        long segmentId,
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
                    var value = x.Value;
                    MarkValueDeleted(ref value);
                    x.Value = value;
                    WriteAheadLog.Append(in key, in value);
                    return AddOrUpdateResult.ADDED;
                },
                (x) =>
                {
                    var value = x.Value;
                    MarkValueDeleted(ref value);
                    x.Value = value;
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
            throw new Exception("MarkFrozen the segment zero first!");

        var (keys, values) = SkipList.ToArray();

        var readOnlySegment =
            new ReadOnlySegment<TKey, TValue>(SegmentId, Options, keys, values);
        return readOnlySegment;
    }

    public void Freeze()
    {
        IsFrozenFlag = true;
        Task.Factory.StartNew(FreezeWriteAheadLog);
    }

    private void FreezeWriteAheadLog()
    {
        while (WritesInProgress > 0)
        {
            Thread.Yield();
        }
        WriteAheadLog.MarkFrozen();
    }

    public void Drop()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Drop();
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
#if USE_LOCK_FREE_SKIP_LIST
        return new LockFreeSkipListIndexedReader<TKey, TValue>(SkipList);
#else
        return new SkipListIndexedReader<TKey, TValue>(SkipList);
#endif
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
#if USE_LOCK_FREE_SKIP_LIST
        return new LockFreeSkipListSeekableIterator<TKey, TValue>(SkipList);
#else
        return new SkipListSeekableIterator<TKey, TValue>(SkipList);
#endif
    }

    public void ReleaseResources()
    {
        WriteAheadLog?.MarkFrozen();
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Dispose();
    }
}
