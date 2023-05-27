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

    public unsafe FixedSizeValueDiskSegment(
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
    }

    public unsafe FixedSizeValueDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataHeaderDevice,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataDevice)
    {
        DataHeaderDevice = dataHeaderDevice;
        EnsureKeyAndValueTypesAreSupported();
        InitKeySizeAndDataLength();
    }

    static unsafe void EnsureKeyAndValueTypesAreSupported()
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

    protected override unsafe TKey ReadKey(long index)
    {
        try
        {
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }
            var headSize = sizeof(KeyHead) + ValueSize;
            var headBytes = DataHeaderDevice.GetBytes(index * headSize, sizeof(KeyHead));
            var head = BinarySerializerHelper.FromByteArray<KeyHead>(headBytes);
            var keyBytes = DataDevice.GetBytes(head.KeyOffset, head.KeyLength);
            return KeySerializer.Deserialize(keyBytes);
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }

    protected override unsafe TValue ReadValue(long index)
    {
        try
        {
            Interlocked.Increment(ref ReadCount);
            if (IsDroppping)
            {
                throw new DiskSegmentIsDroppingException();
            }

            var headSize = sizeof(KeyHead) + ValueSize;
            var valueBytes = DataHeaderDevice
                .GetBytes(index * headSize + sizeof(KeyHead), ValueSize);
            return ValueSerializer.Deserialize(valueBytes);
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
