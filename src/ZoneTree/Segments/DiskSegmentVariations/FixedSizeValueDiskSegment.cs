using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Segments.Model;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.DiskSegmentVariations;

public sealed class FixedSizeValueDiskSegment<TKey, TValue> : DiskSegment<TKey, TValue>
{
    readonly IRandomAccessDevice DataHeaderDevice;

    public override int ReadBufferCount =>
        (DataDevice?.ReadBufferCount ?? 0) + (DataHeaderDevice?.ReadBufferCount ?? 0);

    public FixedSizeValueDiskSegment(
        long segmentId,
        ZoneTreeOptions<TKey, TValue> options) : base(segmentId, options)
    {
        EnsureKeyAndValueTypesAreSupported();
        var randomDeviceManager = options.RandomAccessDeviceManager;
        var diskOptions = options.DiskSegmentOptions;
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
        InitKeySizeAndDataLength();
        LoadDefaultSparseArray();
    }

    public FixedSizeValueDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataHeaderDevice,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataDevice)
    {
        DataHeaderDevice = dataHeaderDevice;
        EnsureKeyAndValueTypesAreSupported();
        InitKeySizeAndDataLength();
        LoadDefaultSparseArray();
    }

    static void EnsureKeyAndValueTypesAreSupported()
    {
        var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        if (hasFixedSizeKey || !hasFixedSizeValue)
        {
            throw new Exception("The FixedSizeKeyDiskSegment requires variable size key and fixed size value.");
        }
    }

    unsafe void InitKeySizeAndDataLength()
    {
        ValueSize = Unsafe.SizeOf<TValue>();
        Length = DataHeaderDevice.Length / (ValueSize + sizeof(KeyHead));
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
            diskOptions.BlockCacheLimit,
            diskOptions.CompressionMethod,
            diskOptions.CompressionLevel,
            diskOptions.BlockCacheReplacementWarningDuration);
        var recordCount = BinarySerializerHelper.FromByteArray<int>(sparseArrayDevice.GetBytes(0, sizeof(int)));
        var offset = sizeof(int);
        var valueSize = ValueSize;
        var sparseArray = new SparseArrayEntry<TKey, TValue>[recordCount];
        for (var i = 0; i < recordCount; ++i)
        {
            var keySize = BinarySerializerHelper.FromByteArray<int>(sparseArrayDevice.GetBytes(offset, sizeof(int)));
            offset += sizeof(int);
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
            diskOptions.BlockCacheLimit,
            true,
            false,
            diskOptions.CompressionMethod,
            diskOptions.CompressionLevel,
            diskOptions.BlockCacheReplacementWarningDuration);
        var sparseArray = SparseArray;
        var recordCount = sparseArray.Count;
        sparseArrayDevice.AppendBytesReturnPosition(BitConverter.GetBytes(recordCount));
        for (var i = 0; i < recordCount; ++i)
        {
            var entry = sparseArray[i];
            var keyBytes = KeySerializer.Serialize(entry.Key);
            sparseArrayDevice.AppendBytesReturnPosition(BitConverter.GetBytes(keyBytes.Length));
            sparseArrayDevice.AppendBytesReturnPosition(keyBytes);
            sparseArrayDevice.AppendBytesReturnPosition(ValueSerializer.Serialize(entry.Value));
            sparseArrayDevice.AppendBytesReturnPosition(BitConverter.GetBytes(entry.Index));
        }
        sparseArrayDevice.SealDevice();
        sparseArrayDevice.Close();
    }

    protected unsafe override TKey ReadKey(long index)
    {
        try
        {
            if (CircularKeyCache.TryGetFromCache(index, out var key)) return key;
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var headSize = sizeof(KeyHead) + ValueSize;
            var headBytes = DataHeaderDevice.GetBytes(index * headSize, sizeof(KeyHead));
            var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
            var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
            key = KeySerializer.Deserialize(keyBytes);
            CircularKeyCache.TryAddToTheCache(index, ref key);
            return key;
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    protected unsafe override TValue ReadValue(long index)
    {
        try
        {
            if (CircularValueCache.TryGetFromCache(index, out var value)) return value;
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }

            var headSize = sizeof(KeyHead) + ValueSize;
            var valueBytes = DataHeaderDevice
                .GetBytes(index * headSize + sizeof(KeyHead), ValueSize);
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
        DataHeaderDevice?.Delete();
        DataDevice?.Delete();
    }

    public override void ReleaseResources()
    {
        DataHeaderDevice?.Dispose();
        DataDevice?.Dispose();
    }

    public override int ReleaseReadBuffers(long ticks)
    {
        var a = DataHeaderDevice?.ReleaseReadBuffers(ticks) ?? 0;
        var b = DataDevice?.ReleaseReadBuffers(ticks) ?? 0;
        return a + b;
    }
}
