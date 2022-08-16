#undef USE_LOCK_FREE_SKIP_LIST
#define USE_BTREE

/*
 * Lock Free Skip List is turned off by default.
 * Because it is slower in most of the test cases.
 * The option is pinned here for future analysis and improvements
 * on lock-free skiplist implementation.
 * It might have advantages when the multi-threaded updates/inserts occur 
 * in different regions in the list.
 */

using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BTree;
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
#if USE_BTREE
    readonly BTree<TKey, TValue> BTree;
#elif USE_LOCK_FREE_SKIP_LIST
    readonly LockFreeSkipList<TKey, TValue> BTree;
#else
    readonly SkipList<TKey, TValue> BTree;
#endif

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public long SegmentId { get; private set; }

    public bool IsFrozen => IsFrozenFlag;

    public bool IsFullyFrozen => IsFrozenFlag && WritesInProgress == 0;

    public int Length => BTree.Length;

    public long MaximumOpIndex => BTree.GetLastOpIndex();

    public IIncrementalIdProvider OpIndexProvider => BTree.OpIndexProvider;

    public MutableSegment(ZoneTreeOptions<TKey, TValue> options,
        long segmentId,
        IIncrementalIdProvider indexOpProvider)
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
#if USE_BTREE
        BTree = new(Comparer,
            Options.BTreeLockMode,
            indexOpProvider);
#else
        BTree = new(Comparer, (int)Math.Log2(options.MutableSegmentMaxItemCount) + 1);
#endif
        MarkValueDeleted = options.MarkValueDeleted;
        MutableSegmentMaxItemCount = options.MutableSegmentMaxItemCount;
    }

    public MutableSegment(
        long segmentId,
        IWriteAheadLog<TKey, TValue> wal,
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<TKey> keys,
        IReadOnlyList<TValue> values,
        long nextOpIndex)
    {
        SegmentId = segmentId;
        WriteAheadLog = wal;
        Options = options;
        Comparer = options.Comparer;
#if USE_BTREE
        BTree = new(Comparer, Options.BTreeLockMode);
        BTree.SetNextOpIndex(nextOpIndex);
#else
        BTree = new(Comparer, (int)Math.Log2(options.MutableSegmentMaxItemCount) + 1);
#endif
        MarkValueDeleted = options.MarkValueDeleted;
        MutableSegmentMaxItemCount = options.MutableSegmentMaxItemCount;
        LoadLogEntries(keys, values);
    }

    void LoadLogEntries(IReadOnlyList<TKey> keys, IReadOnlyList<TValue> values)
    {
        var len = keys.Count;
        for (var i = 0; i < len; ++i)
        {
            var key = keys[i];
            var value = values[i];
            // TODO: Search if we can create faster construction
            // of mutable segment from log entries.
            BTree.Upsert(in key, in value, out var _);
        }
    }

    public bool ContainsKey(in TKey key)
    {
        return BTree.ContainsKey(key);
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        return BTree.TryGetValue(key, out value);
    }

    public AddOrUpdateResult Upsert(in TKey key, in TValue value)
    {
        if (IsFrozenFlag)
            return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (BTree.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;
            var result = BTree.Upsert(in key, in value, out var opIndex);
            WriteAheadLog.Append(in key, in value, opIndex);
            return result ? AddOrUpdateResult.ADDED : AddOrUpdateResult.UPDATED;
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref WritesInProgress);
        }
    }

    public AddOrUpdateResult Delete(in TKey key)
    {
        if (IsFrozenFlag)
            return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (BTree.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;

            TValue insertedValue = default;
#if USE_BTREE
            var status = BTree.AddOrUpdate(key,
                AddOrUpdateResult (ref TValue x) =>
                {
                    MarkValueDeleted(ref x);
                    insertedValue = x;
                    return AddOrUpdateResult.ADDED;
                },
                AddOrUpdateResult (ref TValue x) =>
                {
                    MarkValueDeleted(ref x);
                    insertedValue = x;
                    return AddOrUpdateResult.UPDATED;
                }, out var opIndex);
            WriteAheadLog.Append(in key, in insertedValue, opIndex);
#else
            var status = BTree.AddOrUpdate(key,
                (x) =>
                {
                    var value = x.Value;
                    MarkValueDeleted(ref value);
                    x.Value = value;
                    insertedValue = value;
                    return AddOrUpdateResult.ADDED;
                },
                (x) =>
                {
                    var value = x.Value;
                    MarkValueDeleted(ref value);
                    x.Value = value;
                    insertedValue = value;
                    return AddOrUpdateResult.UPDATED;
                });
            WriteAheadLog.Append(in key, in insertedValue);
#endif
            return status;
        }
        finally
        {
            Interlocked.Decrement(ref WritesInProgress);
        }
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
        BTree.IsReadOnly = true;
    }

    public void Drop()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Drop();
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
#if USE_BTREE
        throw new NotSupportedException("BTree Indexed Reader is not supported.");
#elif USE_LOCK_FREE_SKIP_LIST
        return new LockFreeSkipListIndexedReader<TKey, TValue>(SkipList);
#else
        return new SkipListIndexedReader<TKey, TValue>(SkipList);
#endif
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
#if USE_BTREE
        return IsFullyFrozen ?
            new FrozenBTreeSeekableIterator<TKey, TValue>(BTree) :
            new BTreeSeekableIterator<TKey, TValue>(BTree);
#elif USE_LOCK_FREE_SKIP_LIST
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
