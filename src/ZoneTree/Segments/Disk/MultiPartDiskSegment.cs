using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Compression;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class MultiPartDiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public const CompressionMethod MultiPartHeaderCompressionMethod
        = CompressionMethod.LZ4;

    public const int MultiPartHeaderCompressionLevel
        = CompressionLevels.LZ4Fastest;

    public long SegmentId { get; }

    readonly IRefComparer<TKey> Comparer;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    int IteratorReaderCount;

    bool IsDropRequested = false;

    bool IsDropped = false;

    readonly object DropLock = new();

    readonly IReadOnlyList<IDiskSegment<TKey, TValue>> Parts;

    readonly TKey[] PartKeys;

    readonly TValue[] PartValues;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    public long Length { get; }

    public long MaximumOpIndex => 0;

    public bool IsFullyFrozen => true;

    public bool IsIterativeIndexReader => false;

    public int ReadBufferCount => 0;

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public MultiPartDiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options)
    {
        SegmentId = segmentId;
        Options = options;
        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;

        var randomDeviceManager = options.RandomAccessDeviceManager;
        using var diskSegmentListDevice = randomDeviceManager
                .GetReadOnlyDevice(
                    segmentId,
                    DiskSegmentConstants.MultiPartDiskSegmentCategory,
                    isCompressed: false,
                    compressionBlockSize: 0,
                    maxCachedBlockCount: 0,
                    MultiPartHeaderCompressionMethod,
                    MultiPartHeaderCompressionLevel,
                    blockCacheReplacementWarningDuration: 0);

        if (diskSegmentListDevice.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnceException(
                diskSegmentListDevice.Length, int.MaxValue);

        var len = (int)diskSegmentListDevice.Length;
        var compressedBytes = diskSegmentListDevice.GetBytes(0, len);
        var bytes = DataCompression
            .Decompress(MultiPartHeaderCompressionMethod, compressedBytes);

        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        Parts = ReadParts(options, br);
        PartKeys = ReadKeys(br);
        PartValues = ReadValues(br);
        Length = CalculateLength();
    }

    public static long ReadMaximumSegmentId(
        long segmentId,
        IRandomAccessDeviceManager randomDeviceManager)
    {
        var category = DiskSegmentConstants.MultiPartDiskSegmentCategory;
        if (!randomDeviceManager.DeviceExists(segmentId, category))
            return 0;
        using var diskSegmentListDevice = randomDeviceManager
                .GetReadOnlyDevice(
                    segmentId,
                    category,
                    isCompressed: false,
                    compressionBlockSize: 0,
                    maxCachedBlockCount: 0,
                    MultiPartHeaderCompressionMethod,
                    MultiPartHeaderCompressionLevel,
                    blockCacheReplacementWarningDuration: 0);

        if (diskSegmentListDevice.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnceException(
                diskSegmentListDevice.Length, int.MaxValue);

        var len = (int)diskSegmentListDevice.Length;
        var compressedBytes = diskSegmentListDevice.GetBytes(0, len);
        var bytes = DataCompression.Decompress(MultiPartHeaderCompressionMethod, compressedBytes);

        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        var partCount = br.ReadInt32();
        var parts = new IDiskSegment<TKey, TValue>[partCount];
        var result = segmentId;
        for (var i = 0; i < partCount; ++i)
        {
            var partSegmentId = br.ReadInt64();
            result = Math.Max(partSegmentId, result);
        }
        randomDeviceManager.RemoveReadOnlyDevice(segmentId, category);
        return result;
    }

    public MultiPartDiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<IDiskSegment<TKey, TValue>> parts,
        TKey[] partKeys,
        TValue[] partValues)
    {
        SegmentId = segmentId;
        Options = options;
        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        Parts = parts;
        PartKeys = partKeys;
        PartValues = partValues;
        Length = CalculateLength();
    }

    long CalculateLength()
    {
        var len = Parts.Count;
        var parts = Parts;
        long result = 0;
        for (var i = 0; i < len; ++i)
            result += parts[i].Length;
        return result;
    }

    static IDiskSegment<TKey, TValue>[] ReadParts(
        ZoneTreeOptions<TKey, TValue> options,
        BinaryReader br)
    {
        var partCount = br.ReadInt32();
        var parts = new IDiskSegment<TKey, TValue>[partCount];
        for (var i = 0; i < partCount; ++i)
        {
            var partSegmentId = br.ReadInt64();
            var part = new DiskSegment<TKey, TValue>(partSegmentId, options);
            parts[i] = part;
        }
        return parts;
    }

    TKey[] ReadKeys(BinaryReader br)
    {
        var keyCount = br.ReadInt32();
        var keys = new TKey[keyCount];
        for (var i = 0; i < keyCount; ++i)
        {
            var keyLength = br.ReadInt32();
            var bytes = br.ReadBytes(keyLength);
            keys[i] = KeySerializer.Deserialize(bytes);
        }
        return keys;
    }

    TValue[] ReadValues(BinaryReader br)
    {
        var valueCount = br.ReadInt32();
        var values = new TValue[valueCount];
        for (var i = 0; i < valueCount; ++i)
        {
            var valueLength = br.ReadInt32();
            var bytes = br.ReadBytes(valueLength);
            values[i] = ValueSerializer.Deserialize(bytes);
        }
        return values;
    }

    public IDiskSegment<TKey, TValue> GetPart(int partIndex)
    {
        return Parts[partIndex];
    }

    public TKey[] GetFirstKeysOfEveryPart()
    {
        var len = PartKeys.Length;
        var keys = new TKey[len / 2];
        for (var i = 0; i < len; i += 2)
            keys[i / 2] = PartKeys[i];
        return keys;
    }

    public TKey[] GetLastKeysOfEveryPart()
    {
        var len = PartKeys.Length;
        var keys = new TKey[len / 2];
        for (var i = 0; i < len; i += 2)
            keys[i / 2] = PartKeys[i + 1];
        return keys;
    }

    public TValue[] GetLastValuesOfEveryPart()
    {
        var len = PartValues.Length;
        var values = new TValue[len / 2];
        for (var i = 0; i < len; i += 2)
            values[i / 2] = PartValues[i + 1];
        return values;
    }

    public void AttachIterator()
    {
        lock (DropLock)
        {
            ++IteratorReaderCount;
        }
    }

    public bool ContainsKey(in TKey key)
    {
        var sparseArrayLength = PartKeys.Length;
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        if (found)
            return true;
        if (left == -1 || left == sparseArrayLength - 1)
            return false;

        var partIndex = left / 2;
        return Parts[partIndex].ContainsKey(in key);
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

            var len = Parts.Count;
            for (var i = 0; i < len; ++i)
                Parts[i].Drop();

            DropMeta();

            IsDropped = true;
        }
    }

    public void Drop(HashSet<long> excudedPartIds)
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

            var len = Parts.Count;
            for (var i = 0; i < len; ++i)
            {
                var part = Parts[i];

                if (excudedPartIds.Contains(part.SegmentId))
                    continue;
                part.Drop();
            }

            DropMeta();

            IsDropped = true;
        }
    }

    void DropMeta()
    {
        var randomDeviceManager = Options.RandomAccessDeviceManager;
        using var diskSegmentListDevice = randomDeviceManager
                .GetReadOnlyDevice(
                    SegmentId,
                    DiskSegmentConstants.MultiPartDiskSegmentCategory,
                    isCompressed: false,
                    compressionBlockSize: 0,
                    maxCachedBlockCount: 0,
                    MultiPartHeaderCompressionMethod,
                    MultiPartHeaderCompressionLevel,
                    blockCacheReplacementWarningDuration: 0);

        diskSegmentListDevice.Delete();
        randomDeviceManager
            .RemoveReadOnlyDevice(SegmentId, DiskSegmentConstants.MultiPartDiskSegmentCategory);
    }

    public long GetFirstGreaterOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = PartKeys.Length;
        (var right, var found) = SearchFirstGreaterOrEqualPositionInSparseArray(in key);
        var partIndex = right / 2;
        var diff = right % 2 == 0 ? 0 : 1;
        if (found)
        {
            long off2 = 0;
            var len = partIndex + diff;
            for (var i = 0; i < len; ++i)
                off2 += Parts[i].Length;
            return off2 - diff;
        }
        if (right == sparseArrayLength)
            return Length;
        if (right == -1)
            return Length;

        if (diff == 0)
        {
            long off2 = 0;
            var len = partIndex;
            for (var i = 0; i < len; ++i)
                off2 += Parts[i].Length;
            return off2;
        }

        var position = Parts[partIndex].GetFirstGreaterOrEqualPosition(in key);
        if (position == Parts[partIndex].Length)
            return Length;

        long off = 0;
        for (var i = 0; i < partIndex; ++i)
            off += Parts[i].Length;
        return off + position;
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public TKey GetKey(long index)
    {
        long off = 0;
        var partIndex = 0;
        var len = Parts[partIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++partIndex;
            len = Parts[partIndex].Length;
        }
        var localIndex = index - off;

        if (localIndex == 0)
            return PartKeys[partIndex * 2];
        if (localIndex == len - 1)
            return PartKeys[partIndex * 2 + 1];

        var key = Parts[partIndex].GetKey(localIndex);
        return key;
    }

    public TValue GetValue(long index)
    {
        long off = 0;
        var partIndex = 0;
        var len = Parts[partIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++partIndex;
            len = Parts[partIndex].Length;
        }
        var localIndex = index - off;

        if (localIndex == 0)
            return PartValues[partIndex * 2];
        if (localIndex == len - 1)
            return PartValues[partIndex * 2 + 1];

        return Parts[partIndex].GetValue(localIndex);
    }

    public long GetLastSmallerOrEqualPosition(in TKey key)
    {
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        var partIndex = left / 2;
        var diff = left % 2 == 0 ? 0 : 1;
        if (found)
        {
            long off2 = 0;
            var len = partIndex + diff;
            for (var i = 0; i < len; ++i)
                off2 += Parts[i].Length;
            return off2 - diff;
        }
        if (left == -1)
            return -1;

        if (diff == 1)
        {
            long off2 = 0;
            var len = partIndex;
            for (var i = 0; i <= len; ++i)
                off2 += Parts[i].Length;
            return off2 - 1;
        }

        var position = Parts[partIndex].GetLastSmallerOrEqualPosition(in key);
        if (position == -1)
            return -1;

        long off = 0;
        for (var i = 0; i < partIndex; ++i)
            off += Parts[i].Length;
        return off + position;
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SeekableIterator<TKey, TValue>(this);
    }

    public void InitSparseArray(int size)
    {
        // Don't init sparse array for multi-part segments.
        // Sparse arrays can be individually created per the parts.
    }

    public void LoadIntoMemory()
    {
        var len = Parts.Count;
        for (var i = 0; i < len; ++i)
            Parts[i].LoadIntoMemory();
    }

    public int ReleaseReadBuffers(long ticks)
    {
        var len = Parts.Count;
        var result = 0;
        for (var i = 0; i < len; ++i)
            result += Parts[i].ReleaseReadBuffers(ticks);
        return result;
    }

    public void ReleaseResources()
    {
        var len = Parts.Count;
        for (var i = 0; i < len; ++i)
            Parts[i].ReleaseResources();
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

    public bool TryGet(in TKey key, out TValue value)
    {
        var sparseArrayLength = PartKeys.Length;
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        if (found)
        {
            value = PartValues[left];
            return true;
        }
        if (left == -1 || left == sparseArrayLength - 1)
        {
            value = default;
            return false;
        }

        var partIndex = left / 2;
        return Parts[partIndex].TryGet(in key, out value);
    }

    #region Binary search

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the sparse array or a valid position</returns>
    int FindFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = PartKeys;
        int l = 0, h = list.Length;
        var comp = Comparer;
        while (l < h)
        {
            int mid = l + (h - l) / 2;
            if (comp.Compare(in key, in list[mid]) <= 0)
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
        if (x == PartKeys.Length)
            return x - 1;
        if (Comparer.Compare(in key, in PartKeys[x]) == 0)
            return x;
        return x - 1;
    }

    (int index, bool found) SearchLastSmallerOrEqualPositionInSparseArray(in TKey key)
    {
        var list = PartKeys;
        var len = list.Length;
        if (len == 0)
            return (-1, false);

        var position = FindLastSmallerOrEqualPositionInSparseArray(in key);
        if (position == -1)
            return (-1, false);
        var exactMatch = Comparer.Compare(PartKeys[position], key) == 0;
        return (position, exactMatch);
    }

    (int index, bool found) SearchFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = PartKeys;
        var len = list.Length;
        if (len == 0)
            return (0, false);

        var position = FindFirstGreaterOrEqualPositionInSparseArray(in key);
        if (position == len)
            return (len, false);
        var exactMatch = Comparer.Compare(PartKeys[position], key) == 0;
        return (position, exactMatch);
    }
    #endregion

    public bool IsBeginningOfAPart(long index)
    {
        long off = 0;
        var partIndex = 0;
        var len = Parts[partIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++partIndex;
            len = Parts[partIndex].Length;
        }
        var localIndex = index - off;

        return localIndex == 0;
    }

    public bool IsEndOfAPart(long index)
    {
        long off = 0;
        var partIndex = 0;
        var len = Parts[partIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++partIndex;
            len = Parts[partIndex].Length;
        }
        var localIndex = index - off;

        return localIndex == len - 1;
    }

    public int GetPartIndex(long index)
    {
        long off = 0;
        var partIndex = 0;
        var len = Parts[partIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++partIndex;
            len = Parts[partIndex].Length;
        }
        return partIndex;
    }
}