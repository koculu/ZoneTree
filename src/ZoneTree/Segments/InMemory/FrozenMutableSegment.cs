using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Segments.InMemory;

public sealed class FrozenMutableSegment<TKey, TValue> : IMutableSegment<TKey, TValue>
{
    private IMutableSegment<TKey, TValue> mutableSegment;

    public FrozenMutableSegment(IMutableSegment<TKey, TValue> mutableSegment)
    {
        this.mutableSegment = mutableSegment;
    }

    public bool IsFrozen => true;

    public IIncrementalIdProvider OpIndexProvider => mutableSegment.OpIndexProvider;

    public long SegmentId => mutableSegment.SegmentId;

    public long Length => mutableSegment.Length;

    public long MaximumOpIndex => mutableSegment.MaximumOpIndex;

    public bool IsFullyFrozen => false;

    public bool ContainsKey(in TKey key)
    {
        return mutableSegment.ContainsKey(key);
    }

    public AddOrUpdateResult Delete(in TKey key, out long opIndex)
    {
        opIndex = 0;
        return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;
    }

    public void Drop()
    {
        mutableSegment.Drop();
    }

    public void Freeze()
    {
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        throw new NotSupportedException("BTree Indexed Reader is not supported.");
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator(bool contributeToTheBlockCache = false)
    {
        return mutableSegment.GetSeekableIterator(contributeToTheBlockCache);
    }

    public void ReleaseResources()
    {
        mutableSegment.ReleaseResources();
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        return mutableSegment.TryGet(key, out value);
    }

    public AddOrUpdateResult Upsert(in TKey key, in TValue value, out long opIndex)
    {
        opIndex = 0;
        return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;
    }

    public AddOrUpdateResult Upsert(in TKey key, GetValueDelegate<TKey, TValue> valueGetter, out long opIndex)
    {
        opIndex = 0;
        return AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN;
    }
}
