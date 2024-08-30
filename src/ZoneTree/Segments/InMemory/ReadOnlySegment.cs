using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Block;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments.InMemory;

public sealed class ReadOnlySegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>, IIndexedReader<TKey, TValue>
{
    public long SegmentId { get; }

    public long MaximumOpIndex { get; set; }

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly IReadOnlyList<TKey> SortedKeys;

    readonly IReadOnlyList<TValue> SortedValues;

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public long Length => SortedKeys.Count;

    public bool IsFullyFrozen => true;

    public bool HasNext => true;

    public bool HasPrev => true;

    public bool IsIterativeIndexReader => false;

    public ReadOnlySegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<TKey> sortedKeys,
        IReadOnlyList<TValue> sortedValues)
    {
        SegmentId = segmentId;
        Options = options;
        Comparer = options.Comparer;
        SortedKeys = sortedKeys;
        SortedValues = sortedValues;
        WriteAheadLog = options.WriteAheadLogProvider.GetWAL<TKey, TValue>(
            SegmentId,
            ZoneTree<TKey, TValue>.SegmentWalCategory);
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        int index = BinarySearchAlgorithms
            .BinarySearch(SortedKeys, 0, SortedKeys.Count - 1, Comparer, in key);
        if (index < 0)
        {
            value = default;
            return false;
        }
        value = SortedValues[index];
        return true;
    }

    public bool ContainsKey(in TKey key)
    {
        int index = BinarySearchAlgorithms
            .BinarySearch(SortedKeys, 0, SortedKeys.Count - 1, Comparer, in key);
        return index >= 0;
    }

    public TKey GetKey(long index)
    {
        return SortedKeys[(int)index];
    }

    public TValue GetValue(long index)
    {
        return SortedValues[(int)index];
    }

    public void Drop()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Drop();
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator(bool contributeToTheBlockCache)
    {
        return new SeekableIterator<TKey, TValue>(this, contributeToTheBlockCache);
    }

    /// <summary>
    /// Finds the position of element that is smaller or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>-1 or a valid position</returns>
    public long GetLastSmallerOrEqualPosition(in TKey key) =>
        BinarySearchAlgorithms
            .LastSmallerOrEqualPosition(SortedKeys, 0, SortedKeys.Count - 1, Comparer, in key);

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the segment or a valid position</returns>
    public long GetFirstGreaterOrEqualPosition(in TKey key) =>
        BinarySearchAlgorithms
            .FirstGreaterOrEqualPosition(SortedKeys, 0, SortedKeys.Count - 1, Comparer, in key);

    public void ReleaseResources()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Dispose();
    }

    public bool IsBeginningOfAPart(long index) => false;

    public bool IsEndOfAPart(long index) => false;

    public int GetPartIndex(long index) => -1;

    public TKey GetKey(long index, BlockPin pin)
    {
        return SortedKeys[(int)index];
    }

    public TValue GetValue(long index, BlockPin pin)
    {
        return SortedValues[(int)index];
    }
}
