using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class NullDiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public int Length => 0;

    public int SegmentId { get; }

    public bool IsFullyFrozen => true;

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public bool ContainsKey(in TKey key)
    {
        return false;
    }

    public TKey GetKey(int index)
    {
        throw new IndexOutOfRangeException();
    }

    public TValue GetValue(int index)
    {
        throw new IndexOutOfRangeException();
    }

    public void InitSparseArray(int size)
    {
        // Nothing to init
    }

    public void LoadIntoMemory()
    {
        // Nothing to load
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        value = default;
        return false;
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    public void Drop()
    {
        // Nothing to drop
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public void AddReader()
    {
        // Nothing to add
    }

    public void RemoveReader()
    {
        // Nothing to remove
    }

    public int GetLastSmallerOrEqualPosition(in TKey key)
    {
        return -1;
    }

    public int GetFirstGreaterOrEqualPosition(in TKey key)
    {
        return -1;
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new NullDiskSegmentSeekableIterator<TKey, TValue>();
    }

    public void ReleaseResources()
    {
        // Nothing to release.
    }

    public int ReleaseReadBuffers(long ticks)
    {
        return 0;
    }
}

