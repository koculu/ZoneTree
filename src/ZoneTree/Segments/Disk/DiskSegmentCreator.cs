using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class DiskSegmentCreator<TKey, TValue> : IDiskSegmentCreator<TKey, TValue>
{
    readonly int SegmentId;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    readonly IRandomAccessDevice DataHeaderDevice;

    readonly IRandomAccessDevice DataDevice;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly bool HasFixedSizeKey;

    readonly bool HasFixedSizeValue;

    readonly bool HasFixedSizeKeyAndValue;

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

        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice = randomDeviceManager.CreateWritableDevice(SegmentId, DiskSegmentConstants.DataHeaderCategory);
        DataDevice = randomDeviceManager.CreateWritableDevice(SegmentId, DiskSegmentConstants.DataCategory);
        Options = options;
    }

    public void Append(TKey key, TValue value)
    {
        var keyBytes = KeySerializer.Serialize(key);
        var valueBytes = ValueSerializer.Serialize(value);
        if (HasFixedSizeKeyAndValue)
        {
            DataDevice.AppendBytes(keyBytes);
            DataDevice.AppendBytes(valueBytes);
            return;
        }

        if (HasFixedSizeKey)
        {
            var valueHead = new ValueHead
            {
                ValueLength = valueBytes.Length,
                ValueOffset = DataDevice.AppendBytes(valueBytes)
            };
            DataHeaderDevice.AppendBytes(keyBytes);
            var valueHeadBytes = BinarySerializerHelper.ToByteArray(valueHead);
            DataHeaderDevice.AppendBytes(valueHeadBytes);
            return;
        }

        if (HasFixedSizeValue)
        {
            var keyHead = new KeyHead
            {
                KeyLength = keyBytes.Length,
                KeyOffset = DataDevice.AppendBytes(keyBytes),
            };
            var keyHeadBytes = BinarySerializerHelper.ToByteArray(keyHead);
            DataHeaderDevice.AppendBytes(keyHeadBytes);
            DataHeaderDevice.AppendBytes(valueBytes);
            return;
        }

        var off1 = DataDevice.AppendBytes(keyBytes);
        var off2 = DataDevice.AppendBytes(valueBytes);

        var head = new EntryHead
        {
            KeyLength = keyBytes.Length,
            ValueLength = valueBytes.Length,
            KeyOffset = off1,
            ValueOffset = off2
        };
        var headBytes = BinarySerializerHelper.ToByteArray(head);
        DataHeaderDevice.AppendBytes(headBytes);
    }

    public IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment()
    {
        Close();
        var diskSegment = new DiskSegment<TKey, TValue>(SegmentId, Options);
        return diskSegment;
    }

    public void DropDiskSegment()
    {
        Close();
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice.Delete();
        DataDevice.Delete();
    }

    private void Close()
    {
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice.Close();
        DataDevice.Close();
    }

    public void Dispose()
    {
        if (!HasFixedSizeKeyAndValue)
            DataHeaderDevice.Dispose();
        DataDevice.Dispose();
    }
}