using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Model;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class DiskSegmentCreator<TKey, TValue> : IDiskSegmentCreator<TKey, TValue>
{
    readonly long SegmentId;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    IRandomAccessDevice DataHeaderDevice;

    IRandomAccessDevice DataDevice;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly bool HasFixedSizeKey;

    readonly bool HasFixedSizeValue;

    readonly bool HasFixedSizeKeyAndValue;

    public int Length { get; private set; }

    public bool CanSkipCurrentPart => false;

    public HashSet<long> AppendedPartSegmentIds { get; } = new();

    List<SparseArrayEntry<TKey, TValue>> DefaultSparseArray = new();

    int DefaultSparseArrayStepSize;

    TKey LastKey;

    TValue LastValue;

    public DiskSegmentCreator(
        ZoneTreeOptions<TKey, TValue> options,
        IIncrementalIdProvider incrementalIdProvider
        )
    {
        SegmentId = incrementalIdProvider.NextId();
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        var randomDeviceManager = options.RandomAccessDeviceManager;

        HasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        HasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
        HasFixedSizeKeyAndValue = HasFixedSizeKey && HasFixedSizeValue;
        var diskOptions = options.DiskSegmentOptions;
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice = randomDeviceManager
                .CreateWritableDevice(
                    SegmentId,
                    DiskSegmentConstants.DataHeaderCategory,
                    diskOptions.EnableCompression,
                    diskOptions.CompressionBlockSize,
                    diskOptions.BlockCacheLimit,
                    deleteIfExists: true,
                    backupIfDelete: false,
                    diskOptions.CompressionMethod,
                    diskOptions.CompressionLevel,
                    diskOptions.BlockCacheReplacementWarningDuration);
        DataDevice = randomDeviceManager
            .CreateWritableDevice(
                SegmentId,
                DiskSegmentConstants.DataCategory,
                diskOptions.EnableCompression,
                diskOptions.CompressionBlockSize,
                diskOptions.BlockCacheLimit,
                deleteIfExists: true,
                backupIfDelete: false,
                diskOptions.CompressionMethod,
                diskOptions.CompressionLevel,
                diskOptions.BlockCacheReplacementWarningDuration);
        Options = options;
        DefaultSparseArrayStepSize = options.DiskSegmentOptions.DefaultSparseArrayStepSize;
    }

    public void Append(TKey key, TValue value, IteratorPosition iteratorPosition)
    {
        LastKey = key;
        LastValue = value;
        if (DefaultSparseArrayStepSize > 0 && Length % DefaultSparseArrayStepSize == 0)
        {
            DefaultSparseArray.Add(new(key, value, Length));
        }
        ++Length;
        var keyBytes = KeySerializer.Serialize(key);
        var valueBytes = ValueSerializer.Serialize(value);
        if (HasFixedSizeKeyAndValue)
        {
            DataDevice.AppendBytesReturnPosition(keyBytes);
            DataDevice.AppendBytesReturnPosition(valueBytes);
            return;
        }

        if (HasFixedSizeKey)
        {
            var valueHead = new ValueHead
            {
                ValueLength = valueBytes.Length,
                ValueOffset = DataDevice.AppendBytesReturnPosition(valueBytes)
            };
            DataHeaderDevice.AppendBytesReturnPosition(keyBytes);
            var valueHeadBytes = BinarySerializerHelper.ToByteArray(valueHead);
            DataHeaderDevice.AppendBytesReturnPosition(valueHeadBytes);
            return;
        }

        if (HasFixedSizeValue)
        {
            var keyHead = new KeyHead
            {
                KeyLength = keyBytes.Length,
                KeyOffset = DataDevice.AppendBytesReturnPosition(keyBytes),
            };
            var keyHeadBytes = BinarySerializerHelper.ToByteArray(keyHead);
            DataHeaderDevice.AppendBytesReturnPosition(keyHeadBytes);
            DataHeaderDevice.AppendBytesReturnPosition(valueBytes);
            return;
        }

        var off1 = DataDevice.AppendBytesReturnPosition(keyBytes);
        var off2 = DataDevice.AppendBytesReturnPosition(valueBytes);

        var head = new EntryHead
        {
            KeyLength = keyBytes.Length,
            ValueLength = valueBytes.Length,
            KeyOffset = off1,
            ValueOffset = off2
        };
        var headBytes = BinarySerializerHelper.ToByteArray(head);
        DataHeaderDevice.AppendBytesReturnPosition(headBytes);
    }

    public IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment()
    {
        if (DefaultSparseArrayStepSize > 0 && DefaultSparseArray.LastOrDefault().Index + 1 != Length)
        {
            DefaultSparseArray.Add(new(LastKey, LastValue, Length - 1));
        }
        DataHeaderDevice?.SealDevice();
        DataDevice?.SealDevice();
        DataHeaderDevice?.ReleaseReadBuffers(0);
        DataDevice?.ReleaseReadBuffers(0);
        var diskSegment = DiskSegmentFactory.CreateDiskSegment<TKey, TValue>(
            SegmentId, Options,
            DataHeaderDevice, DataDevice);
        if (DefaultSparseArray.Count > 0)
            diskSegment.SetDefaultSparseArray(DefaultSparseArray);
        DataHeaderDevice = null;
        DataDevice = null;
        return diskSegment;
    }

    public void DropDiskSegment()
    {
        Close();
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice.Delete();
        DataDevice.Delete();
    }

    void Close()
    {
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice?.Close();
        DataDevice?.Close();
    }

    public void Dispose()
    {
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice?.Dispose();
        DataDevice?.Dispose();
    }

    public void Append(IDiskSegment<TKey, TValue> part, TKey key1, TKey key2, TValue value1, TValue value2)
    {
        throw new NotSupportedException("This method should be called on MultiDiskSegmentCreator.");
    }
}