using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class VariableSizeDiskSegment<TKey, TValue> : DiskSegment<TKey, TValue>
{
    public unsafe VariableSizeDiskSegment(
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
        InitDataLength();
    }

    public unsafe VariableSizeDiskSegment(long segmentId,
        ZoneTreeOptions<TKey, TValue> options,
        IRandomAccessDevice dataHeaderDevice,
        IRandomAccessDevice dataDevice) : base(segmentId, options, dataHeaderDevice, dataDevice)
    {
        EnsureKeyAndValueTypesAreSupported();
        InitDataLength();
    }

    static unsafe void EnsureKeyAndValueTypesAreSupported()
    {
        var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        if (hasFixedSizeKey || hasFixedSizeValue)
        {
            throw new Exception("The FixedSizeKeyDiskSegment requires variable size key and variable size value.");
        }
    }

    unsafe void InitDataLength()
    {
        Length = DataHeaderDevice.Length / sizeof(EntryHead);
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
            var headBytes = DataHeaderDevice.GetBytes(index * sizeof(EntryHead), sizeof(KeyHead));
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

            var headBytes = DataHeaderDevice.GetBytes((long)index * sizeof(EntryHead) + sizeof(KeyHead), sizeof(ValueHead));
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
