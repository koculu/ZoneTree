using System.Runtime.CompilerServices;
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

    CircularCache<TKey> CircularCache { get; }

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
                diskOptions.BlockCacheLimit,
                diskOptions.CompressionMethod,
                diskOptions.CompressionLevel,
                diskOptions.BlockCacheReplacementWarningDuration);
        InitKeyAndValueSizeAndDataLength();
        CircularCache = new CircularCache<TKey>(diskOptions.KeyCacheSize, diskOptions.KeyCacheRecordLifeTimeInMillisecond);
    }

    public FixedSizeKeyAndValueDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataDevice)
    {
        EnsureKeyAndValueTypesAreSupported();
        InitKeyAndValueSizeAndDataLength();
        var diskOptions = options.DiskSegmentOptions;
        CircularCache = new CircularCache<TKey>(diskOptions.KeyCacheSize, diskOptions.KeyCacheRecordLifeTimeInMillisecond);
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

    protected override TKey ReadKey(long index)
    {
        try
        {
            if (CircularCache.TryGetFromCache(index, out var key)) return key;
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var itemSize = KeySize + ValueSize;
            var keyBytes = DataDevice.GetBytes(itemSize * index, KeySize);
            key = KeySerializer.Deserialize(keyBytes);
            CircularCache.TryAddToTheCache(index, ref key);
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
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var itemSize = KeySize + ValueSize;
            var valueBytes = DataDevice.GetBytes(itemSize * index + KeySize, ValueSize);
            return ValueSerializer.Deserialize(valueBytes);
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
        return DataDevice?.ReleaseReadBuffers(ticks) ?? 0;
    }
}