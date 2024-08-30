using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Block;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public abstract class DiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public long SegmentId { get; }

    readonly IRefComparer<TKey> Comparer;

    protected readonly ISerializer<TKey> KeySerializer;

    protected readonly ISerializer<TValue> ValueSerializer;

    protected IRandomAccessDevice DataDevice;

    protected int KeySize;

    protected int ValueSize;

    protected IReadOnlyList<SparseArrayEntry<TKey, TValue>> SparseArray = Array.Empty<SparseArrayEntry<TKey, TValue>>();

    int IteratorReaderCount;

    protected volatile int ReadCount;

    volatile bool IsDropRequested;

    protected volatile bool IsDroppping;

    bool IsDropped;

    readonly object DropLock = new();

    public long Length { get; protected set; }

    public long MaximumOpIndex => 0;

    public bool IsFullyFrozen => true;

    public bool IsIterativeIndexReader => false;

    public abstract int ReadBufferCount { get; }

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public CircularCache<TKey> CircularKeyCache { get; }

    public CircularCache<TValue> CircularValueCache { get; }

    protected ZoneTreeOptions<TKey, TValue> Options;

    protected DiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
        SegmentId = segmentId;
        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        var diskOptions = options.DiskSegmentOptions;
        CircularKeyCache = new CircularCache<TKey>(
            diskOptions.KeyCacheSize,
            diskOptions.KeyCacheRecordLifeTimeInMillisecond);
        CircularValueCache = new CircularCache<TValue>(
            diskOptions.ValueCacheSize,
            diskOptions.ValueCacheRecordLifeTimeInMillisecond);
    }

    protected DiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataDevice)
    {
        Options = options;
        SegmentId = segmentId;
        DataDevice = dataDevice;

        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        var diskOptions = options.DiskSegmentOptions;
        CircularKeyCache = new CircularCache<TKey>(
            diskOptions.KeyCacheSize,
            diskOptions.KeyCacheRecordLifeTimeInMillisecond);
        CircularValueCache = new CircularCache<TValue>(
            diskOptions.ValueCacheSize,
            diskOptions.ValueCacheRecordLifeTimeInMillisecond);
    }

    public bool ContainsKey(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length - 1;

        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
            if (found)
                return true;
            if (index == -1 || index == sparseArrayLength - 1)
                return false;
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index - 1;
        }
        var res = BinarySearchAlgorithms.BinarySearch(ReadKey, lower, upper, Comparer, in key);
        return res >= 0;
    }

    public TKey GetKey(long index)
    {
        return ReadKey(index);
    }

    public TValue GetValue(long index)
    {
        return ReadValue(index);
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length - 1;

        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
            if (found)
            {
                value = SparseArray[index].Value;
                return true;
            }
            if (index == -1 || index == sparseArrayLength - 1)
            {
                value = default;
                return false;
            }
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index - 1;
        }
        var result = BinarySearchAlgorithms.BinarySearch(ReadKey, lower, upper, Comparer, in key);
        if (result < 0)
        {
            value = default;
            return false;
        }
        value = GetValue(result);
        return true;
    }

    public void InitSparseArray(int size)
    {
        var len = Length;
        size = (int)Math.Min(size, len);
        if (size < 1)
        {
            SparseArray = Array.Empty<SparseArrayEntry<TKey, TValue>>();
            return;
        }
        var step = (int)(len / size);
        if (step < 1)
            return;
        var sparseArray = new List<SparseArrayEntry<TKey, TValue>>();
        for (int i = 0; i < len; i += step)
        {
            var sparseArrayEntry = CreateSparseArrayEntry(i);
            sparseArray.Add(sparseArrayEntry);
        }
        if (sparseArray[^1].Index != len - 1)
        {
            var sparseArrayEntry = CreateSparseArrayEntry(len - 1);
            sparseArray.Add(sparseArrayEntry);
        }
        SparseArray = sparseArray;
    }

    public void LoadIntoMemory()
    {
        InitSparseArray((int)Math.Min(Length, int.MaxValue));
    }

    SparseArrayEntry<TKey, TValue> CreateSparseArrayEntry(long index)
    {
        // Optimisation possibility? read key and value together to reduce IO calls.
        var key = ReadKey(index);
        var value = ReadValue(index);
        var sparseArrayEntry = new SparseArrayEntry<TKey, TValue>(key, value, index);
        return sparseArrayEntry;
    }

    protected TKey ReadKey(long index) => ReadKey(index, null);

    protected TValue ReadValue(long index) => ReadValue(index, null);

    protected abstract TKey ReadKey(long index, BlockPin pin);

    protected abstract TValue ReadValue(long index, BlockPin pin);

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the sparse array or a valid position</returns>
    int FindFirstGreaterOrEqualPositionInSparseArray(TKey key)
    {
        var list = SparseArray;
        var comp = Comparer;

        int compareKeyByIndex(int index)
        {
            return comp.Compare(in list[index].Key, in key);
        }
        return BinarySearchAlgorithms.FirstGreaterOrEqualPosition(compareKeyByIndex, 0, list.Count - 1);
    }

    /// <summary>
    /// Finds the position of element that is smaller or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>-1 or a valid position</returns>
    int FindLastSmallerOrEqualPositionInSparseArray(TKey key)
    {
        var list = SparseArray;
        var comp = Comparer;

        int compareKeyByIndex(int index)
        {
            return comp.Compare(in list[index].Key, in key);
        }
        return BinarySearchAlgorithms.LastSmallerOrEqualPosition(compareKeyByIndex, 0, list.Count - 1);
    }

    (int index, bool found) SearchLastSmallerOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SparseArray;
        var len = list.Count;
        if (len == 0)
            return (-1, false);

        var position = FindLastSmallerOrEqualPositionInSparseArray(key);
        if (position == -1)
            return (-1, false);
        var exactMatch = Comparer.Compare(SparseArray[position].Key, key) == 0;
        return (position, exactMatch);
    }

    (int index, bool found) SearchFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SparseArray;
        var len = list.Count;
        if (len == 0)
            return (0, false);

        var position = FindFirstGreaterOrEqualPositionInSparseArray(key);
        if (position == len)
            return (len, false);
        var exactMatch = Comparer.Compare(SparseArray[position].Key, key) == 0;
        return (position, exactMatch);
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

    public IDiskSegment<TKey, TValue> GetPart(int partIndex)
    {
        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        ReleaseResources();
    }

    public void Drop()
    {
        lock (DropLock)
        {
            if (IsDropped)
                return;
            if (IteratorReaderCount > 0)
            {
                // iterators are long-lived objects.
                // Cancel the drop, and let the iterators
                // call drop when they are disposed.
                IsDropRequested = true;
                return;
            }

            // reads will increase ReadCount when they begin,
            // and decrease ReadCount when they end.
            IsDroppping = true;
            // After the flag change,
            // reads will start throwing DiskSegmentIsDroppingException

            // Delay the drop operation until all reads finalized
            // either with success or exception.
            if (ReadCount > 0)
            {
                // Synchronize reads with drop operation.
                SpinWait.SpinUntil(() => ReadCount == 0);
            }

            // No active reads remaining.
            // Safe to drop.

            DeleteDevices();
            IsDropped = true;
        }
    }

    protected abstract void DeleteDevices();

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public void AttachIterator()
    {
        lock (DropLock)
        {
            ++IteratorReaderCount;
        }
    }

    public void DetachIterator()
    {
        lock (DropLock)
        {
            --IteratorReaderCount;
            if (IsDropRequested && IteratorReaderCount == 0)
            {
                try
                {
                    Drop();
                }
                catch (Exception e)
                {
                    DropFailureReporter?.Invoke(this, e);
                }
            }
        }
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator(bool contributeToTheBlockCache)
    {
        return new SeekableIterator<TKey, TValue>(this, contributeToTheBlockCache);
    }

    public long GetLastSmallerOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length - 1;
        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
            if (found)
                return SparseArray[index].Index;
            if (index == -1)
                return -1;
            if (index == sparseArrayLength - 1)
                return SparseArray[index].Index;
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index - 1;
        }
        return BinarySearchAlgorithms.LastSmallerOrEqualPosition(ReadKey, lower, upper, Comparer, in key);
    }

    public long GetFirstGreaterOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length - 1;
        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchFirstGreaterOrEqualPositionInSparseArray(in key);
            if (found)
                return SparseArray[index].Index;
            if (index == sparseArrayLength)
                return Length;
            if (index == 0)
                return SparseArray[index].Index;
            if (index > 0)
                --index;
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index - 1;
        }
        return BinarySearchAlgorithms.FirstGreaterOrEqualPosition(ReadKey, lower, upper, Comparer, in key);
    }

    public virtual void ReleaseResources()
    {
        DataDevice?.Dispose();
    }

    public abstract int ReleaseReadBuffers(long ticks);

    public void Drop(HashSet<long> excludedPartIds)
    {
        if (excludedPartIds.Contains(SegmentId))
            return;
        Drop();
    }

    public bool IsBeginningOfAPart(long index) => false;

    public bool IsEndOfAPart(long index) => false;

    public int GetPartIndex(long index) => -1;

    public int GetPartCount() => 0;

    abstract public void SetDefaultSparseArray(IReadOnlyList<SparseArrayEntry<TKey, TValue>> defaultSparseArray);

    public int ReleaseCircularKeyCacheRecords()
    {
        return CircularKeyCache.ReleaseInactiveCacheRecords();
    }

    public int ReleaseCircularValueCacheRecords()
    {
        return CircularKeyCache.ReleaseInactiveCacheRecords();
    }

    public TKey GetKey(long index, BlockPin pin)
    {
        return ReadKey(index, pin);
    }

    public TValue GetValue(long index, BlockPin pin)
    {
        return ReadValue(index, pin);
    }
}
