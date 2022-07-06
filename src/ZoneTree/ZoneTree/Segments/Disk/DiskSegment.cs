using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tenray.Collections;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;

namespace ZoneTree.Segments.Disk;

public class DiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public int SegmentId { get; }

    private IRefComparer<TKey> Comparer;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    private readonly bool HasFixedSizeKey;

    private readonly bool HasFixedSizeValue;

    private readonly bool HasFixedSizeKeyAndValue;

    readonly IRandomAccessDevice DataHeaderDevice;

    readonly IRandomAccessDevice DataDevice;

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    private int KeySize;

    private int ValueSize;

    IReadOnlyList<SparseArrayEntry<TKey, TValue>> SparseArray = Array.Empty<SparseArrayEntry<TKey,TValue>>();

    int ReaderCount;

    bool IsDropRequested = false;

    bool IsDropped = false;

    object DropLock = new ();

    public int Length { get; }

    public bool IsFullyFrozen => true;

    public bool HasNext => true;

    public bool HasPrev => true;

    public bool IsIterativeIndexReader => false;

    public unsafe DiskSegment(
        int segmentId,
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

        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice = randomDeviceManager.GetReadOnlyDevice(SegmentId, DiskSegmentConstants.DataHeaderCategory);
        DataDevice = randomDeviceManager.GetReadOnlyDevice(SegmentId, DiskSegmentConstants.DataCategory);

        KeySize = Unsafe.SizeOf<TKey>();
        ValueSize = Unsafe.SizeOf<TValue>();
        if (HasFixedSizeKeyAndValue)
        {
            Length = (int)(DataDevice.Length / (KeySize + ValueSize));
        }
        else if (HasFixedSizeKey)
        {
            Length = (int)(DataHeaderDevice.Length / (KeySize + sizeof(ValueHead)));
        }
        else if (HasFixedSizeValue)
        {
            Length = (int)(DataHeaderDevice.Length / (ValueSize + sizeof(KeyHead)));
        }
        else
        {
            Length = (int)(DataHeaderDevice.Length / sizeof(EntryHead));
        }
    }

    public bool ContainsKey(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        var lower = 0;
        var upper = Length - 1;

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

        int res = BinarySearch(in key, lower, upper);
        return res >= 0;
    }

    public TKey GetKey(int index)
    {
        return ReadKey(index);
    }

    public TValue GetValue(int index)
    {
        return ReadValue(index);
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        var sparseArrayLength = SparseArray.Count;
        var lower = 0;
        var upper = Length - 1;

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

        int result = BinarySearch(in key, lower, upper);
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
        size = Math.Min(size, len);
        if (size < 1)
            return;
        var step = len / size;
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
        InitSparseArray(Length);
    }

    private unsafe SparseArrayEntry<TKey, TValue> CreateSparseArrayEntry(int index)
    {
        // Optimisation possibility? read key and value together to reduce IO calls.
        var key = ReadKey(index);
        var value = ReadValue(index);
        var sparseArrayEntry = new SparseArrayEntry<TKey, TValue>(key, value, index);
        return sparseArrayEntry;
    }

    private unsafe TKey ReadKey(int index)
    {
        if (HasFixedSizeKeyAndValue)
        {
            var itemSize = KeySize + ValueSize;
            var keyBytes = DataDevice.GetBytes(itemSize * index, KeySize);
            return KeySerializer.Deserialize(keyBytes);
        }
        else if (HasFixedSizeKey)
        {
            var headSize = sizeof(ValueHead) + KeySize;
            var keyBytes = DataHeaderDevice.GetBytes((long)index * headSize, KeySize);
            return KeySerializer.Deserialize(keyBytes);
        }
        else if(HasFixedSizeValue)
        {
            var headSize = sizeof(KeyHead) + ValueSize;
            var headBytes = DataHeaderDevice.GetBytes((long)index * headSize, sizeof(KeyHead));
            var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
            var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
            return KeySerializer.Deserialize(keyBytes);
        }
        else
        {
            var headBytes = DataHeaderDevice.GetBytes((long)index * sizeof(EntryHead), sizeof(KeyHead));
            var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
            var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
            return KeySerializer.Deserialize(keyBytes);
        }
    }

    private unsafe TValue ReadValue(int index)
    {
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
    private int BinarySearch(in TKey key, int lower, int upper)
    {
        var comp = Comparer;
        int l = lower, r = upper;
        while (l <= r)
        {
            int m = l + (r - l) / 2;

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
            if (ReaderCount > 0)
            {
                IsDropRequested = true;
                return;
            }
            IsDropRequested = false;
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

    public void AddReader()
    {
        Interlocked.Increment(ref ReaderCount);
    }

    public void RemoveReader()
    {
        Interlocked.Decrement(ref ReaderCount);
        if (IsDropRequested && ReaderCount == 0) {
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

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SeekableIterator<TKey, TValue>(this);
    }

    int GetLastSmallerOrEqualPosition(in TKey key, int l, int h)
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

    int GetFirstGreaterOrEqualPosition(in TKey key, int l, int h)
    {
        // This is the lower bound algorithm.
        var comp = Comparer;
        while (l < h)
        {
            int mid = l + (h - l) / 2;
            var rec = ReadKey(mid);
            if (comp.Compare(in key, in rec) <= 0)
                h = mid;
            else
                l = mid + 1;
        }
        return l;
    }

    public int GetLastSmallerOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        var lower = 0;
        var upper = Length;
        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
            if (found)
                return SparseArray[index].Index;
            if (index == -1)
                return -1;
            if(index == sparseArrayLength - 1)
                return SparseArray[index].Index;
            lower = SparseArray[index].Index;
            upper = SparseArray[index + 1].Index;
        }
        return GetLastSmallerOrEqualPosition(in key, lower, upper);
    }

    public int GetFirstGreaterOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SparseArray.Count;
        var lower = 0;
        var upper = Length;
        if (sparseArrayLength != 0)
        {
            (var index, var found) = SearchFirstGreaterOrEqualPositionInSparseArray(in key);
            if (found)
                return SparseArray[index].Index;
            if (index == sparseArrayLength)
                return upper;
            if (index == sparseArrayLength - 1)
                return SparseArray[index].Index;
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
}
