
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments;

public sealed class MutableSegment<TKey, TValue> : IMutableSegment<TKey, TValue>
{
    readonly ZoneTreeOptions<TKey, TValue> Options;

    volatile bool IsFrozenFlag = false;

    volatile int WritesInProgress = 0;

    readonly MarkValueDeletedDelegate<TValue> MarkValueDeleted;

    readonly int MutableSegmentMaxItemCount;

    readonly BTree<TKey, TValue> BTree;

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public long SegmentId { get; private set; }

    public bool IsFrozen => IsFrozenFlag;

    public bool IsFullyFrozen => IsFrozenFlag && BTree.IsReadOnly;

    public long Length => BTree.Length;

    public long MaximumOpIndex => BTree.GetLastOpIndex();

    public IIncrementalIdProvider OpIndexProvider => BTree.OpIndexProvider;

    public MutableSegment(ZoneTreeOptions<TKey, TValue> options,
        long segmentId,
        IIncrementalIdProvider indexOpProvider)
    {
        SegmentId = segmentId;
        Options = options;
        WriteAheadLog = options.WriteAheadLogProvider
            .GetOrCreateWAL(
                SegmentId,
                ZoneTree<TKey, TValue>.SegmentWalCategory,
                options.WriteAheadLogOptions,
                options.KeySerializer,
                options.ValueSerializer);
        Comparer = options.Comparer;

        BTree = new(Comparer,
            Options.BTreeLockMode,
            indexOpProvider);

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

        BTree = new(Comparer, Options.BTreeLockMode);
        BTree.SetNextOpIndex(nextOpIndex);

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
        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (IsFrozenFlag)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

            if (BTree.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;
            var result = BTree.Upsert(in key, in value, out var opIndex);
            WriteAheadLog.Append(in key, in value, opIndex);
            return result ? AddOrUpdateResult.ADDED : AddOrUpdateResult.UPDATED;
        }
        finally
        {
            Interlocked.Decrement(ref WritesInProgress);
        }
    }

    public AddOrUpdateResult Delete(in TKey key)
    {
        try
        {
            Interlocked.Increment(ref WritesInProgress);

            if (IsFrozenFlag)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;

            if (BTree.Length >= MutableSegmentMaxItemCount)
                return AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;

            TValue insertedValue = default;

            var status = BTree
                .AddOrUpdate(key,
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
        new Thread(FreezeWriteAheadLog).Start();
    }

    void FreezeWriteAheadLog()
    {
        try
        {
            while (WritesInProgress > 0)
            {
                Thread.Yield();
            }
            WriteAheadLog.MarkFrozen();
            BTree.SetTreeReadOnlyAndLockFree();

        }
        catch(Exception e)
        {
            Options.Logger.LogError(e);
        }
    }

    public void Drop()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Drop();
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        throw new NotSupportedException("BTree Indexed Reader is not supported.");
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return IsFullyFrozen ?
            new FrozenBTreeSeekableIterator<TKey, TValue>(BTree) :
            new BTreeSeekableIterator<TKey, TValue>(BTree);
    }

    public void ReleaseResources()
    {
        WriteAheadLog?.MarkFrozen();
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Dispose();
    }
}
