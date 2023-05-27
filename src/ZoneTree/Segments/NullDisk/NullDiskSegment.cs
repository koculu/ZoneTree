﻿using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.NullDisk;

public sealed class NullDiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public long Length => 0;

    public long MaximumOpIndex => 0;

    public long SegmentId { get; }

    public bool IsFullyFrozen => true;

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public int ReadBufferCount => 0;

    public bool ContainsKey(in TKey key)
    {
        return false;
    }

    public TKey GetKey(long index)
    {
        throw new IndexOutOfRangeException();
    }

    public TValue GetValue(long index)
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

    public IDiskSegment<TKey, TValue> GetPart(int partIndex)
    {
        return null;
    }

    public TKey[] GetFirstKeysOfEveryPart()
    {
        return Array.Empty<TKey>();
    }

    public TKey[] GetLastKeysOfEveryPart()
    {
        return Array.Empty<TKey>();
    }

    public TValue[] GetLastValuesOfEveryPart()
    {
        return Array.Empty<TValue>();
    }


    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public void AttachIterator()
    {
        // Nothing to add
    }

    public void DetachIterator()
    {
        // Nothing to remove
    }

    public long GetLastSmallerOrEqualPosition(in TKey key)
    {
        return -1;
    }

    public long GetFirstGreaterOrEqualPosition(in TKey key)
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

    public void Drop(HashSet<long> excludedPartIds)
    {
    }

    public bool IsBeginningOfAPart(long index) => false;

    public bool IsEndOfAPart(long index) => false;

    public int GetPartIndex(long index) => -1;
}

