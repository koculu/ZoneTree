using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class MultiSectorDiskSegment<TKey, TValue> : IDiskSegment<TKey, TValue>
{
    public int SegmentId { get; }

    readonly IRefComparer<TKey> Comparer;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    int ReaderCount;

    bool IsDropRequested = false;

    bool IsDropped = false;

    readonly object DropLock = new();

    readonly IReadOnlyList<IDiskSegment<TKey, TValue>> Sectors;

    readonly TKey[] SectorKeys;
    
    readonly TValue[] SectorValues;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    public int Length { get; }

    public bool IsFullyFrozen => true;

    public bool IsIterativeIndexReader => false;

    public int ReadBufferCount => 0;

    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    public MultiSectorDiskSegment(
        int segmentId,
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
                    DiskSegmentConstants.MultiSectorDiskSegmentCategory, false, 0, 0);

        if (diskSegmentListDevice.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnceException(
                diskSegmentListDevice.Length, int.MaxValue);

        var len = (int)diskSegmentListDevice.Length;
        var compressedBytes = diskSegmentListDevice.GetBytes(0, len);
        var bytes = DataCompression.Decompress(compressedBytes);

        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);
        Sectors = ReadSectors(options, br);
        SectorKeys = ReadKeys(br);
        SectorValues = ReadValues(br);
        Length = CalculateLength();
    }

    public static int ReadMaximumSegmentId(
        int segmentId,
        IRandomAccessDeviceManager randomDeviceManager)
    {
        var category = DiskSegmentConstants.MultiSectorDiskSegmentCategory;
        if (!randomDeviceManager.DeviceExists(segmentId, category))
            return 0;
        using var diskSegmentListDevice = randomDeviceManager
                .GetReadOnlyDevice(
                    segmentId,
                    category, false, 0, 0);

        if (diskSegmentListDevice.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnceException(
                diskSegmentListDevice.Length, int.MaxValue);
        
        var len = (int)diskSegmentListDevice.Length;
        var compressedBytes = diskSegmentListDevice.GetBytes(0, len);
        var bytes = DataCompression.Decompress(compressedBytes);

        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        var sectorCount = br.ReadInt32();
        var sectors = new IDiskSegment<TKey, TValue>[sectorCount];
        var result = segmentId;
        for (var i = 0; i < sectorCount; ++i)
        {
            var sectorSegmentId = br.ReadInt32();
            result = Math.Max(sectorSegmentId, result);
        }
        randomDeviceManager.RemoveReadOnlyDevice(segmentId, category);
        return result;
    }

    public MultiSectorDiskSegment(
        int segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IReadOnlyList<IDiskSegment<TKey, TValue>> sectors,
        TKey[] sectorKeys,
        TValue[] sectorValues)
    {
        SegmentId = segmentId;
        Options = options;
        Comparer = options.Comparer;
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        Sectors = sectors;
        SectorKeys = sectorKeys;
        SectorValues = sectorValues;
        Length = CalculateLength();
    }

    int CalculateLength()
    {
        var len = Sectors.Count;
        var sectors = Sectors;
        var result = 0;
        for (var i = 0; i < len; ++i)
            result += sectors[i].Length;
        return result;
    }

    static IDiskSegment<TKey, TValue>[] ReadSectors(
        ZoneTreeOptions<TKey, TValue> options,
        BinaryReader br)
    {
        var sectorCount = br.ReadInt32();
        var sectors = new IDiskSegment<TKey, TValue>[sectorCount];
        for (var i = 0; i < sectorCount; ++i)
        {
            var sectorSegmentId = br.ReadInt32();
            var sector = new DiskSegment<TKey, TValue>(sectorSegmentId, options);
            sectors[i] = sector;
        }
        return sectors;
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

    public IDiskSegment<TKey, TValue> GetSector(int sectorIndex)
    {
        return Sectors[sectorIndex];
    }

    public TKey[] GetFirstKeysOfEverySector()
    {
        var len = SectorKeys.Length;
        var keys = new TKey[len / 2];
        for (var i = 0; i < len; i += 2)
            keys[i / 2] = SectorKeys[i];
        return keys;
    }

    public TKey[] GetLastKeysOfEverySector()
    {
        var len = SectorKeys.Length;
        var keys = new TKey[len / 2];
        for (var i = 0; i < len; i += 2)
            keys[i / 2] = SectorKeys[i + 1];
        return keys;
    }

    public TValue[] GetLastValuesOfEverySector()
    {
        var len = SectorValues.Length;
        var values = new TValue[len / 2];
        for (var i = 0; i < len; i += 2)
            values[i / 2] = SectorValues[i + 1];
        return values;
    }

    public void AddReader()
    {
        Interlocked.Increment(ref ReaderCount);
    }

    public bool ContainsKey(in TKey key)
    {
        var sparseArrayLength = SectorKeys.Length;
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        if (found)
            return true;
        if (left == -1 || left == sparseArrayLength - 1)
            return false;

        var sectorIndex = left / 2;
        return Sectors[sectorIndex].ContainsKey(in key);
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

            var len = Sectors.Count;
            for (var i = 0; i < len; ++i)
                Sectors[i].Drop();

            DropMeta();

            IsDropped = true;
        }
    }


    public void Drop(HashSet<int> exludedSectorIds)
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

            var len = Sectors.Count;
            for (var i = 0; i < len; ++i)
            {
                var sector = Sectors[i];

                if (exludedSectorIds.Contains(sector.SegmentId))
                    continue;
                sector.Drop();
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
                    DiskSegmentConstants.MultiSectorDiskSegmentCategory, false, 0, 0);
        diskSegmentListDevice.Delete();
        randomDeviceManager
            .RemoveReadOnlyDevice(SegmentId, DiskSegmentConstants.MultiSectorDiskSegmentCategory);
    }

    public int GetFirstGreaterOrEqualPosition(in TKey key)
    {
        var sparseArrayLength = SectorKeys.Length;
        (var right, var found) = SearchFirstGreaterOrEqualPositionInSparseArray(in key);
        var sectorIndex = right / 2;
        var isStartOfSector = right % 2 == 0 ? 1 : 0;
        if (found)
        {
            var off2 = 0;
            var len = sectorIndex + isStartOfSector;
            for (var i = 0; i < len; ++i)
                off2 += Sectors[i].Length;
            return off2 - isStartOfSector;
        }
        if (right == sparseArrayLength)
            return Length;
        if (right == -1)
            return Length;

        if (isStartOfSector == 1) {
            var off2 = 0;
            var len = sectorIndex;
            for (var i = 0; i < len; ++i)
                off2 += Sectors[i].Length;
            return off2 - isStartOfSector;
        }

        var position = Sectors[sectorIndex].GetFirstGreaterOrEqualPosition(in key);
        if (position == Sectors[sectorIndex].Length)
            return Length;

        var off = 0;
        for (var i = 0; i < sectorIndex; ++i)
            off += Sectors[i].Length;
        return off + position;
    }

    public IIndexedReader<TKey, TValue> GetIndexedReader()
    {
        return this;
    }

    public TKey GetKey(int index)
    {
        var off = 0;
        var sectorIndex = 0;
        var len = Sectors[sectorIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++sectorIndex;
            len = Sectors[sectorIndex].Length;
        }
        var localIndex = index - off;

        if (localIndex == 0)
            return SectorKeys[sectorIndex*2];
        if (localIndex == len - 1)
            return SectorKeys[sectorIndex * 2 + 1];

        var key = Sectors[sectorIndex].GetKey(localIndex);
        return key;
    }

    public TValue GetValue(int index)
    {
        var off = 0;
        var sectorIndex = 0;
        var len = Sectors[sectorIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++sectorIndex;
            len = Sectors[sectorIndex].Length;
        }
        var localIndex = index - off;

        if (localIndex == 0)
            return SectorValues[sectorIndex * 2];
        if (localIndex == len - 1)
            return SectorValues[sectorIndex * 2 + 1];

        return Sectors[sectorIndex].GetValue(localIndex);
    }

    public int GetLastSmallerOrEqualPosition(in TKey key)
    {
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        var sectorIndex = left / 2;
        var isStartOfSector = left % 2 == 0 ? 1 : 0;
        if (found)
        {
            var off2 = 0;
            var len = sectorIndex + isStartOfSector;
            for (var i = 0; i < len; ++i)
                off2 += Sectors[i].Length;
            return off2 - isStartOfSector;
        }
        if (left == -1)
            return -1;

        if (isStartOfSector == 1)
        {
            var off2 = 0;
            var len = sectorIndex + 1;
            for (var i = 0; i < len; ++i)
                off2 += Sectors[i].Length;
            return off2 - isStartOfSector;
        }

        var position = Sectors[sectorIndex].GetLastSmallerOrEqualPosition(in key);
        if (position == -1)
            return -1;

        var off = 0;
        for (var i = 0; i < sectorIndex; ++i)
            off += Sectors[i].Length;
        return off + position;
    }

    public ISeekableIterator<TKey, TValue> GetSeekableIterator()
    {
        return new SeekableIterator<TKey, TValue>(this);
    }

    public void InitSparseArray(int size)
    {
    }

    public void LoadIntoMemory()
    {
        var len = Sectors.Count;
        for (var i = 0; i < len; ++i)
            Sectors[i].LoadIntoMemory();
    }

    public int ReleaseReadBuffers(long ticks)
    {
        var len = Sectors.Count;
        var result = 0;
        for (var i = 0; i < len; ++i)
            result += Sectors[i].ReleaseReadBuffers(ticks);
        return result;
    }

    public void ReleaseResources()
    {
        var len = Sectors.Count;
        for (var i = 0; i < len; ++i)
            Sectors[i].ReleaseResources();
    }

    public void RemoveReader()
    {
        Interlocked.Decrement(ref ReaderCount);
        if (IsDropRequested && ReaderCount == 0)
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

    public bool TryGet(in TKey key, out TValue value)
    {
        var sparseArrayLength = SectorKeys.Length;
        (var left, var found) = SearchLastSmallerOrEqualPositionInSparseArray(in key);
        if (found)
        {
            value = SectorValues[left];
            return true;
        }
        if (left == -1 || left == sparseArrayLength - 1)
        {
            value = default;
            return false;
        }

        var sectorIndex = left / 2;
        return Sectors[sectorIndex].TryGet(in key, out value);
    }

    #region Binary search

    /// <summary>
    /// Finds the position of element that is greater or equal than key.
    /// </summary>
    /// <param name="key">The key</param>
    /// <returns>The length of the sparse array or a valid position</returns>
    int FindFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SectorKeys;
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
        if (x == SectorKeys.Length)
            return x - 1;
        if (Comparer.Compare(in key, in SectorKeys[x]) == 0)
            return x;
        return x - 1;
    }

    (int index, bool found) SearchLastSmallerOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SectorKeys;
        var len = list.Length;
        if (len == 0)
            return (-1, false);

        var position = FindLastSmallerOrEqualPositionInSparseArray(in key);
        if (position == -1)
            return (-1, false);
        var exactMatch = Comparer.Compare(SectorKeys[position], key) == 0;
        return (position, exactMatch);
    }

    (int index, bool found) SearchFirstGreaterOrEqualPositionInSparseArray(in TKey key)
    {
        var list = SectorKeys;
        var len = list.Length;
        if (len == 0)
            return (0, false);

        var position = FindFirstGreaterOrEqualPositionInSparseArray(in key);
        if (position == len)
            return (len, false);
        var exactMatch = Comparer.Compare(SectorKeys[position], key) == 0;
        return (position, exactMatch);
    }
    #endregion

    public bool IsBeginningOfASector(int index)
    {
        var off = 0;
        var sectorIndex = 0;
        var len = Sectors[sectorIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++sectorIndex;
            len = Sectors[sectorIndex].Length;
        }
        var localIndex = index - off;

        return localIndex == 0;
    }

    public bool IsEndOfASector(int index)
    {
        var off = 0;
        var sectorIndex = 0;
        var len = Sectors[sectorIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++sectorIndex;
            len = Sectors[sectorIndex].Length;
        }
        var localIndex = index - off;

        return localIndex == len - 1;
    }

    public int GetSectorIndex(int index)
    {
        var off = 0;
        var sectorIndex = 0;
        var len = Sectors[sectorIndex].Length;
        while (off + len <= index)
        {
            off += len;
            ++sectorIndex;
            len = Sectors[sectorIndex].Length;
        }
        return sectorIndex;
    }
}