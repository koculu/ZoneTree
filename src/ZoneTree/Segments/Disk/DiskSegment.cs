using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class DiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public long SegmentId { get; }

    readonly IRefComparer<TKey> Comparer;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    readonly bool HasFixedSizeKey;

    readonly bool HasFixedSizeValue;

    readonly bool HasFixedSizeKeyAndValue;

    readonly IRandomAccessDevice DataHeaderDevice;

    readonly IRandomAccessDevice DataDevice;

    readonly int KeySize;

    readonly int ValueSize;

    IReadOnlyList<SparseArrayEntry<TKey, TValue>> SparseArray = Array.Empty<SparseArrayEntry<TKey, TValue>>();

    int IteratorReaderCount;

    volatile int ReadCount;

    volatile bool IsDropRequested = false;

    volatile bool IsDroppping = false;

    bool IsDropped = false;

    readonly object DropLock = new();

    public long Length { get; }

    public long MaximumOpIndex => 0;

    public bool IsFullyFrozen => true;

    public bool IsIterativeIndexReader => false;

    public int ReadBufferCount => 
                    (DataDevice?.ReadBufferCount ?? 0) +
                    (DataHeaderDevice?.ReadBufferCount ?? 0);

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public unsafe DiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options)
    {
        SegmentId = segmentId;
        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;

        HasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        HasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        HasFixedSizeKeyAndValue = HasFixedSizeKey && HasFixedSizeValue;

        var randomDeviceManager = options.RandomAccessDeviceManager;
        var diskOptions = options.DiskSegmentOptions;
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice = randomDeviceManager
                .GetReadOnlyDevice(
                    SegmentId,
                    DiskSegmentConstants.DataHeaderCategory,
                    diskOptions.EnableCompression,
                    diskOptions.CompressionBlockSize,
                    diskOptions.BlockCacheLimit,
                    diskOptions.CompressionMethod,
                    diskOptions.CompressionLevel,
                    diskOptions.BlockCacheReplacementWarningDuration
                    );
        DataDevice = randomDeviceManager
            .GetReadOnlyDevice(
                SegmentId,
                DiskSegmentConstants.DataCategory,
                diskOptions.EnableCompression,
                diskOptions.CompressionBlockSize,
                diskOptions.BlockCacheLimit,
                diskOptions.CompressionMethod,
                diskOptions.CompressionLevel,
                diskOptions.BlockCacheReplacementWarningDuration);

        KeySize = Unsafe.SizeOf<TKey>();
        ValueSize = Unsafe.SizeOf<TValue>();
        if (HasFixedSizeKeyAndValue)
        {
            Length = DataDevice.Length / (KeySize + ValueSize);
        }
        else if (HasFixedSizeKey)
        {
            Length = DataHeaderDevice.Length / (KeySize + sizeof(ValueHead));
        }
        else if (HasFixedSizeValue)
        {
            Length = DataHeaderDevice.Length / (ValueSize + sizeof(KeyHead));
        }
        else
        {
            Length = DataHeaderDevice.Length / sizeof(EntryHead);
        }
    }

    public unsafe DiskSegment(long segmentId, 
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataHeaderDevice,
        IRandomAccessDevice dataDevice)
    {
        SegmentId = segmentId;
        DataHeaderDevice = dataHeaderDevice;
        DataDevice = dataDevice;

        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;

        HasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        HasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        HasFixedSizeKeyAndValue = HasFixedSizeKey && HasFixedSizeValue;
        KeySize = Unsafe.SizeOf<TKey>();
        ValueSize = Unsafe.SizeOf<TValue>();

        if (HasFixedSizeKeyAndValue)
        {
            Length = DataDevice.Length / (KeySize + ValueSize);
        }
        else if (HasFixedSizeKey)
        {
            Length = DataHeaderDevice.Length / (KeySize + sizeof(ValueHead));
        }
        else if (HasFixedSizeValue)
        {
            Length = DataHeaderDevice.Length / (ValueSize + sizeof(KeyHead));
        }
        else
        {
            Length = DataHeaderDevice.Length / sizeof(EntryHead);
        }
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
            upper = SparseArray[index + 1].Index;
        }

        var res = BinarySearch(in key, lower, upper);
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

        var result = BinarySearch(in key, lower, upper);
        if (result < 0)
        {
            value = default;
            return false;
        }
        value = GetValue(result);
        return true;
    }

    public unsafe void InitSparseArray(int size)
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

    unsafe SparseArrayEntry<TKey, TValue> CreateSparseArrayEntry(long index)
    {
        // Optimisation possibility? read key and value together to reduce IO calls.
        var key = ReadKey(index);
        var value = ReadValue(index);
        var sparseArrayEntry = new SparseArrayEntry<TKey, TValue>(key, value, index);
        return sparseArrayEntry;
    }

    unsafe TKey ReadKey(long index)
    {
        try
        {
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            if (HasFixedSizeKeyAndValue)
            {
                var itemSize = KeySize + ValueSize;
                var keyBytes = DataDevice.GetBytes(itemSize * index, KeySize);
                return KeySerializer.Deserialize(keyBytes);
            }
            else if (HasFixedSizeKey)
            {
                var headSize = sizeof(ValueHead) + KeySize;
                var keyBytes = DataHeaderDevice.GetBytes(index * headSize, KeySize);
                return KeySerializer.Deserialize(keyBytes);
            }
            else if (HasFixedSizeValue)
            {
                var headSize = sizeof(KeyHead) + ValueSize;
                var headBytes = DataHeaderDevice.GetBytes(index * headSize, sizeof(KeyHead));
                var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
                var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
                return KeySerializer.Deserialize(keyBytes);
            }
            else
            {
                var headBytes = DataHeaderDevice.GetBytes(index * sizeof(EntryHead), sizeof(KeyHead));
                var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
                var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
                return KeySerializer.Deserialize(keyBytes);
            }
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    unsafe TValue ReadValue(long index)
    {
        try
        {
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            if (HasFixedSizeKeyAndValue)
            {
                var itemSize = KeySize + ValueSize;
                var valueBytes = DataDevice.GetBytes(itemSize * index + KeySize, ValueSize);
                return ValueSerializer.Deserialize(valueBytes);
            }
            else if (HasFixedSizeKey)
            {
                var headSize = sizeof(ValueHead) + KeySize;
                var headBytes = DataHeaderDevice
                    .GetBytes((long)index * headSize + KeySize, sizeof(ValueHead));
                var head = BinarySerializerHelper.FromByteArray<ValueHead>(headBytes);
                var valueBytes = DataDevice.GetBytes(head.ValueOffset, head.ValueLength);
                return ValueSerializer.Deserialize(valueBytes);
            }
            else if (HasFixedSizeValue)
            {
                var headSize = sizeof(KeyHead) + ValueSize;
                var valueBytes = DataHeaderDevice
                    .GetBytes((long)index * headSize + sizeof(KeyHead), ValueSize);
                return ValueSerializer.Deserialize(valueBytes);
            }
            else
            {
                var headBytes = DataHeaderDevice.GetBytes((long)index * sizeof(EntryHead) + sizeof(KeyHead), sizeof(ValueHead));
                var head = BinarySerializerHelper.FromByteArray<ValueHead>(headBytes);
                var valueBytes = DataDevice.GetBytes(head.ValueOffset, head.ValueLength);
                return ValueSerializer.Deserialize(valueBytes);
            }
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the sparse array or a valid position</returns>
    int FindFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SparseArray;
        int l = 0, h = list.Count;
        var comp = Comparer;
        while (l < h)
        {
            int mid = l + (h - l) / 2;
            if (comp.Compare(in key, in list[mid].Key) <= 0)
                h = mid;
            else
                l = mid + 1;
        }
        return l;
    }

    /// <summary>
    /// Finds the position of element that is smaller or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>-1 or a valid position</returns>
    int FindLastSmallerOrEqualPositionInSparseArray(in TKey key)
    {
        var x = FindFirstGreaterOrEqualPositionInSparseArray(in key);
        if (x == -1)
            return -1;
        if (x == SparseArray.Count)
            return x - 1;
        if (Comparer.Compare(in key, in SparseArray[x].Key) == 0)
            return x;
        return x - 1;
    }

    (int index, bool found) SearchLastSmallerOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SparseArray;
        var len = list.Count;
        if (len == 0)
            return (-1, false);

        var position = FindLastSmallerOrEqualPositionInSparseArray(in key);
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

        var position = FindFirstGreaterOrEqualPositionInSparseArray(in key);
        if (position == len)
            return (len, false);
        var exactMatch = Comparer.Compare(SparseArray[position].Key, key) == 0;
        return (position, exactMatch);
    }

    /// <summary>
    /// Search the key and returns its position or -1 if not found.
    /// </summary>
    /// <param name="key">Key to search</param>
    /// <param name="lower">Lower inclusive</param>
    /// <param name="upper">Upper inclusive</param>
    /// <returns>-1 or the position of the key.</returns>
    long BinarySearch(in TKey key, long lower, long upper)
    {
        var comp = Comparer;
        long l = lower, r = upper;
        while (l <= r)
        {
            var m = l + (r - l) / 2;

            // Check if key is present at mid
            var rec = ReadKey(m);
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

            if (!HasFixedSizeKeyAndValue)
                DataHeaderDevice.Delete();
            DataDevice.Delete();
            IsDropped = true;
        }
    }

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

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SeekableIterator<TKey, TValue>(this);
    }

    long GetLastSmallerOrEqualPosition(in TKey key, long l, long h)
    {
        var x = GetFirstGreaterOrEqualPosition(in key, l, h);
        if (x == -1)
            return -1;
        if (x == Length)
            return x - 1;
        var rec = ReadKey(x);
        if (Comparer.Compare(in key, in rec) == 0)
            return x;
        return x - 1;
    }

    long GetFirstGreaterOrEqualPosition(in TKey key, long l, long h)
    {
        // This is the lower bound algorithm.
        var comp = Comparer;
        while (l < h)
        {
            var mid = l + (h - l) / 2;
            var rec = ReadKey(mid);
            if (comp.Compare(in key, in rec) <= 0)
                h = mid;
            else
                l = mid + 1;
        }
        return l;
    }

    public long GetLastSmallerOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length;
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
            upper = SparseArray[index + 1].Index;
        }
        return GetLastSmallerOrEqualPosition(in key, lower, upper);
    }

    public long GetFirstGreaterOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        long lower = 0;
        long upper = Length;
        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchFirstGreaterOrEqualPositionInSparseArray(in key);
            if (found)
                return SparseArray[index].Index;
            if (index == sparseArrayLength)
                return upper;
            if (index == 0)
                return SparseArray[index].Index;
            if (index > 0)
                --index;
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index;
        }
        return GetFirstGreaterOrEqualPosition(in key, lower, upper);
    }

    public void ReleaseResources()
    {
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice.Dispose();
        DataDevice.Dispose();
    }

    public int ReleaseReadBuffers(long ticks)
    {
        var a = DataHeaderDevice?.ReleaseReadBuffers(ticks) ?? 0;
        var b = DataDevice?.ReleaseReadBuffers(ticks) ?? 0;
        return a + b;
    }

    public void Drop(HashSet<long> excludedPartIds)
    {
        if (excludedPartIds.Contains(SegmentId))
            return;
        Drop();
    }

    public bool IsBeginningOfAPart(long index) => false;

    public bool IsEndOfAPart(long index) => false;

    public int GetPartIndex(long index) => -1;
}
