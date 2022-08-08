using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments;

public class ReadOnlySegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>, IIndexedReader<TKey, TValue>
{
    public int SegmentId { get; }

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly IReadOnlyList<TKey> SortedKeys;

    readonly IReadOnlyList<TValue> SortedValues;

    readonly IRefComparer<TKey> Comparer;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    public int Length => SortedKeys.Count;

    public bool IsFullyFrozen => true;

    public bool HasNext => true;

    public bool HasPrev => true;

    public bool IsIterativeIndexReader => false;

    public ReadOnlySegment(
        int segmentId,
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
        int index = BinarySearch(in key);
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
        int index = BinarySearch(in key);
        return index >= 0;
    }

    private int BinarySearch(in TKey key)
    {
        var list = SortedKeys;
        var comp = Comparer;
        int l = 0, r = list.Count - 1;
        while (l <= r)
        {
            int m = l + (r - l) / 2;

            // Check if key is present at mid
            var rec = list[m];
            var res = comp.Compare(in rec, in key);
            if (res == 0)
                return m;

            // If key greater, ignore left half
            if (res < 0)
                l = m + 1;

            // If x is smaller, ignore right half
            else
                r = m - 1;
        }
        // if we reach here, then element was
        // not present
        return -1;
    }

    public TKey GetKey(int index)
    {
        return SortedKeys[index];
    }

    public TValue GetValue(int index)
    {
        return SortedValues[index];
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

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SeekableIterator<TKey, TValue>(this);
    }

    /// <summary>
    /// Finds the position of element that is smaller or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>-1 or a valid position</returns>
    public int GetLastSmallerOrEqualPosition(in TKey key)
    {
        var x = GetFirstGreaterOrEqualPosition(in key);
        if (x == -1)
            return -1;
        if (x == SortedKeys.Count)
            return x - 1;
        if (Comparer.Compare(in key, SortedKeys[x]) == 0)
            return x;
        return x - 1;
    }

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the segment or a valid position</returns>
    public int GetFirstGreaterOrEqualPosition(in TKey key)
    {
        // This is the lower bound algorithm.
        var list = SortedKeys;
        int l = 0, h = list.Count;
        var comp = Comparer;
        while (l < h)
        {
            int mid = l + (h - l) / 2;
            if (comp.Compare(in key, list[mid]) <= 0)
                h = mid;
            else
                l = mid + 1;
        }
        return l;
    }

    public void ReleaseResources()
    {
        Options.WriteAheadLogProvider.RemoveWAL(SegmentId, ZoneTree<TKey, TValue>.SegmentWalCategory);
        WriteAheadLog?.Dispose();
    }

    public bool IsBeginningOfASector(int index) => false;

    public bool IsEndOfASector(int index) => false;

    public int GetSectorIndex(int index) => -1;
}
