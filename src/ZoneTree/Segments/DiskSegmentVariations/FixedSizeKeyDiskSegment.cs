using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Segments.Model;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.DiskSegmentVariations;

public sealed class FixedSizeKeyDiskSegment<TKey, TValue> : DiskSegment<TKey, TValue>
{
    public unsafe FixedSizeKeyDiskSegment(
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

    public unsafe FixedSizeKeyDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataHeaderDevice,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataHeaderDevice, dataDevice)
    {
        EnsureKeyAndValueTypesAreSupported();
        InitKeySizeAndDataLength();
    }

    static unsafe void EnsureKeyAndValueTypesAreSupported()
    {
        var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        if (!hasFixedSizeKey || hasFixedSizeValue)
        {
            throw new Exception("The FixedSizeKeyDiskSegment requires fixed size key and variable size value.");
        }
    }

    unsafe void InitKeySizeAndDataLength()
    {
        KeySize = Unsafe.SizeOf<TKey>();
        Length = DataHeaderDevice.Length / (KeySize + sizeof(ValueHead));
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
            var headSize = sizeof(ValueHead) + KeySize;
            var keyBytes = DataHeaderDevice.GetBytes(index * headSize, KeySize);
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

            var headSize = sizeof(ValueHead) + KeySize;
            var headBytes = DataHeaderDevice
                .GetBytes(index * headSize + KeySize, sizeof(ValueHead));
            var head = BinarySerializerHelper.FromByteArray<ValueHead>(headBytes);
            var valueBytes = DataDevice.GetBytes(head.ValueOffset, head.ValueLength);
            return ValueSerializer.Deserialize(valueBytes);
        }
        finally
        {
            Interlocked.Decrement(ref ReadCount);
        }
    }
}
