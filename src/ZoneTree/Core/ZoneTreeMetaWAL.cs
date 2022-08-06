using System.Runtime.InteropServices;
using System.Text.Json;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTreeMetaWAL<TKey, TValue> : IDisposable
{
    const int ZoneTreeMetaId = 0;

    const string MetaWalCategory = ".meta.wal";

    const string MetaFileCategory = ".json";

    IRandomAccessDevice Device { get; set; }

    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public bool IsReadOnly { get; }

    public ZoneTreeMetaWAL(ZoneTreeOptions<TKey, TValue> options, bool isReadOnly)
    {
        Options = options;
        IsReadOnly = isReadOnly;
        if (IsReadOnly)
        {
            Device = Options
                .RandomAccessDeviceManager
                .GetReadOnlyDevice(ZoneTreeMetaId, MetaWalCategory, false, 0);
        }
        else
        {

            Device = Options
                .RandomAccessDeviceManager
                .CreateWritableDevice(ZoneTreeMetaId, MetaWalCategory, false, 0);
        }
    }

    public static bool Exists(ZoneTreeOptions<TKey, TValue> options)
    {
        return options
            .RandomAccessDeviceManager
            .DeviceExists(
                ZoneTreeMetaId,
                MetaFileCategory);

    }

    public void EnqueueReadOnlySegment(int segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.EnqueueReadOnlySegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void DequeueReadOnlySegment(int segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.DequeueReadOnlySegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void NewSegmentZero(int segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.NewSegmentZero,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void NewDiskSegment(int segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.NewDiskSegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void ClearContent()
    {
        Device.ClearContent();
    }

    public void DeleteFile()
    {
        Device.Delete();
    }

    public void Dispose()
    {
        if (IsReadOnly)
        {
            Options
                .RandomAccessDeviceManager
                .RemoveReadOnlyDevice(ZoneTreeMetaId, MetaWalCategory);
        }
        else
        {
            Options
                .RandomAccessDeviceManager
                .RemoveWritableDevice(ZoneTreeMetaId, MetaWalCategory);
        }
        Device?.Dispose();
        Device = null;
    }

    private void AppendRecord(in MetaWalRecord record)
    {
        lock (this)
        {
            var bytes = BinarySerializerHelper.ToByteArray(record);
            Device.AppendBytesReturnPosition(bytes);
        }
    }

    public unsafe IReadOnlyList<MetaWalRecord> GetAllRecords()
    {
        var len = Device.Length;
        if (len > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnce(len, int.MaxValue);
        var bytes = Device.GetBytes(0, (int)len);
        var list = new List<MetaWalRecord>();
        var off = 0;
        while (off < len)
        {
            var record = BinarySerializerHelper.FromByteArray<MetaWalRecord>(bytes, off);
            list.Add(record);
            off += sizeof(MetaWalRecord);
        }
        return list;
    }

    public void SaveMetaData(
        ZoneTreeMeta zoneTreeMeta,
        int segmentZero,
        int diskSegment,
        int[] readOnlySegments)
    {
        var newZoneTreeMeta = new ZoneTreeMeta
        {
            KeyType = zoneTreeMeta.KeyType,
            ValueType = zoneTreeMeta.ValueType,
            ComparerType = zoneTreeMeta.ComparerType,
            DiskSegment = diskSegment,
            ReadOnlySegments = readOnlySegments,
            SegmentZero = segmentZero,
            KeySerializerType = zoneTreeMeta.KeySerializerType,
            ValueSerializerType = zoneTreeMeta.ValueSerializerType,
            WriteAheadLogMode = zoneTreeMeta.WriteAheadLogMode,
            EnableDiskSegmentCompression = zoneTreeMeta.EnableDiskSegmentCompression,
            DiskSegmentCompressionBlockSize = zoneTreeMeta.DiskSegmentCompressionBlockSize,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            newZoneTreeMeta,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });
        var deviceManager = Options.RandomAccessDeviceManager;

        using var device = deviceManager
            .CreateWritableDevice(ZoneTreeMetaId, MetaFileCategory, false, 0);

        // If crash occurs during following 3 operations,
        // the tree meta file would become corrupted.
        // However, it is possible to recover the tree meta file by traversing 
        // the WAL and Segment files. Because of that
        // we don't implement a backup logic here.

        // 1. clear the target json meta file.
        device.ClearContent();
        // 2. save the json meta file
        device.AppendBytesReturnPosition(bytes);
        // 3. clear the meta WAL file
        ClearContent();

        device.Close();
        deviceManager.RemoveWritableDevice(device.SegmentId, MetaFileCategory);
        zoneTreeMeta.SegmentZero = segmentZero;
        zoneTreeMeta.DiskSegment = diskSegment;
        zoneTreeMeta.ReadOnlySegments = readOnlySegments;
    }

    public static ZoneTreeMeta LoadZoneTreeMetaWithoutWALRecords(
        IRandomAccessDeviceManager deviceManager)
    {
        using var device = deviceManager
            .GetReadOnlyDevice(ZoneTreeMetaId, MetaFileCategory, false, 0); 
        if (device.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnce(device.Length, int.MaxValue);
        var bytes = device.GetBytes(0, (int)device.Length);
        device.Close();
        deviceManager.RemoveReadOnlyDevice(device.SegmentId, MetaFileCategory);
        var meta = JsonSerializer.Deserialize<ZoneTreeMeta>(bytes);
        return meta;
    }
}

public enum MetaWalOperation
{
    EnqueueReadOnlySegment,
    DequeueReadOnlySegment,
    NewSegmentZero,
    NewDiskSegment
}

[StructLayout(LayoutKind.Sequential)]
public struct MetaWalRecord
{
    public MetaWalOperation Operation;
    public int SegmentId;
}