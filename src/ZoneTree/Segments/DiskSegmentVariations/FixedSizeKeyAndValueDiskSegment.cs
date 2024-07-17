using System.Runtime.CompilerServices;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.DiskSegmentVariations;

public sealed class FixedSizeKeyAndValueDiskSegment<TKey, TValue> : DiskSegment<TKey, TValue>
{
    public override int ReadBufferCount =>
        (DataDevice?.ReadBufferCount ?? 0);

    public FixedSizeKeyAndValueDiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options) : base(segmentId, options)
    {
        EnsureKeyAndValueTypesAreSupported();
        var randomDeviceManager = options.RandomAccessDeviceManager;
        var diskOptions = options.DiskSegmentOptions;
        DataDevice = randomDeviceManager
            .GetReadOnlyDevice(
                SegmentId,
                DiskSegmentConstants.DataCategory,
                diskOptions.EnableCompression,
                diskOptions.CompressionBlockSize,
                diskOptions.CompressionMethod,
                diskOptions.CompressionLevel);
        InitKeyAndValueSizeAndDataLength();
        LoadDefaultSparseArray();
    }

    public FixedSizeKeyAndValueDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataDevice)
    {
        EnsureKeyAndValueTypesAreSupported();
        InitKeyAndValueSizeAndDataLength();
        LoadDefaultSparseArray();
    }

    static void EnsureKeyAndValueTypesAreSupported()
    {
        var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        if (!hasFixedSizeKey || !hasFixedSizeValue)
        {
            throw new Exception("The FixedSizeKeyDiskSegment requires fixed size key and fixed size value.");
        }
    }

    void InitKeyAndValueSizeAndDataLength()
    {
        KeySize = Unsafe.SizeOf<TKey>();
        ValueSize = Unsafe.SizeOf<TValue>();
        Length = DataDevice.Length / (KeySize + ValueSize);
    }

    void LoadDefaultSparseArray()
    {
        var options = Options;
        var diskOptions = options.DiskSegmentOptions;
        if (diskOptions.DefaultSparseArrayStepSize == 0) return;
        var deviceManager = options.RandomAccessDeviceManager;
        if (!deviceManager.DeviceExists(
            SegmentId,
            DiskSegmentConstants.SparseArrayCategory,
            diskOptions.EnableCompression))
            return;
        using var sparseArrayDevice = deviceManager.GetReadOnlyDevice(
            SegmentId,
            DiskSegmentConstants.SparseArrayCategory,
            diskOptions.EnableCompression,
            diskOptions.CompressionBlockSize,
            diskOptions.CompressionMethod,
            diskOptions.CompressionLevel);
        var recordCount = BinarySerializerHelper.FromByteArray<int>(sparseArrayDevice.GetBytes(0, sizeof(int)));
        var offset = sizeof(int);
        var keySize = KeySize;
        var valueSize = ValueSize;
        var sparseArray = new SparseArrayEntry<TKey, TValue>[recordCount];
        for (var i = 0; i < recordCount; ++i)
        {
            var key = KeySerializer.Deserialize(sparseArrayDevice.GetBytes(offset, keySize));
            offset += keySize;
            var value = ValueSerializer.Deserialize(sparseArrayDevice.GetBytes(offset, valueSize));
            offset += valueSize;
            var index = BinarySerializerHelper.FromByteArray<int>(sparseArrayDevice.GetBytes(offset, sizeof(long)));
            offset += sizeof(long);
            var entry = new SparseArrayEntry<TKey, TValue>(key, value, index);
            sparseArray[i] = entry;
        }
        sparseArrayDevice.Close();
    }

    public override void SetDefaultSparseArray(IReadOnlyList<SparseArrayEntry<TKey, TValue>> defaultSparseArray)
    {
        SparseArray = defaultSparseArray;
        var options = Options;
        var diskOptions = options.DiskSegmentOptions;
        var deviceManager = options.RandomAccessDeviceManager;
        using var sparseArrayDevice = deviceManager.CreateWritableDevice(
            SegmentId,
            DiskSegmentConstants.SparseArrayCategory,
            diskOptions.EnableCompression,
            diskOptions.CompressionBlockSize,
            true,
            false,
            diskOptions.CompressionMethod,
            diskOptions.CompressionLevel);
        var sparseArray = SparseArray;
        var recordCount = sparseArray.Count;
        sparseArrayDevice.AppendBytesReturnPosition(BitConverter.GetBytes(recordCount));
        for (var i = 0; i < recordCount; ++i)
        {
            var entry = sparseArray[i];
            sparseArrayDevice.AppendBytesReturnPosition(KeySerializer.Serialize(entry.Key));
            sparseArrayDevice.AppendBytesReturnPosition(ValueSerializer.Serialize(entry.Value));
            sparseArrayDevice.AppendBytesReturnPosition(BitConverter.GetBytes(entry.Index));
        }
        sparseArrayDevice.SealDevice();
        sparseArrayDevice.Close();
    }

    protected override TKey ReadKey(long index)
    {
        try
        {
            if (CircularKeyCache.TryGetFromCache(index, out var key)) return key;
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var itemSize = KeySize + ValueSize;
            var keyBytes = DataDevice.GetBytes(itemSize * index, KeySize);
            key = KeySerializer.Deserialize(keyBytes);
            CircularKeyCache.TryAddToTheCache(index, ref key);
            return key;
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    protected override TValue ReadValue(long index)
    {
        try
        {
            if (CircularValueCache.TryGetFromCache(index, out var value)) return value;
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var itemSize = KeySize + ValueSize;
            var valueBytes = DataDevice.GetBytes(itemSize * index + KeySize, ValueSize);
            value = ValueSerializer.Deserialize(valueBytes);
            CircularValueCache.TryAddToTheCache(index, ref value);
            return value;
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    protected override void DeleteDevices()
    {
        DataDevice?.Delete();
    }

    public override void ReleaseResources()
    {
        DataDevice?.Dispose();
    }

    public override int ReleaseReadBuffers(long ticks)
    {
        return DataDevice?.ReleaseInactiveCachedBuffers(ticks) ?? 0;
    }
}