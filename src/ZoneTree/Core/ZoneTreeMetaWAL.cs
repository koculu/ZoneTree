using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.RandomAccess;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTreeMetaWAL<TKey, TValue> : IDisposable
{
    const CompressionMethod MetaWALCompressionMethod = CompressionMethod.None;

    const int MetaWALCompressionLevel = 0;

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
                .GetReadOnlyDevice(
                    ZoneTreeMetaId,
                    MetaWalCategory,
                    isCompressed: false,
                    compressionBlockSize: 0,
                    MetaWALCompressionMethod,
                    MetaWALCompressionLevel);
        }
        else
        {

            Device = Options
                .RandomAccessDeviceManager
                .CreateWritableDevice(
                    ZoneTreeMetaId,
                    MetaWalCategory,
                    isCompressed: false,
                    compressionBlockSize: 0,
                    deleteIfExists: false,
                    backupIfDelete: false,
                    MetaWALCompressionMethod,
                    MetaWALCompressionLevel);
        }
    }

    public static bool Exists(ZoneTreeOptions<TKey, TValue> options)
    {
        return options
            .RandomAccessDeviceManager
            .DeviceExists(
                ZoneTreeMetaId,
                MetaFileCategory, false);

    }

    public void EnqueueReadOnlySegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.EnqueueReadOnlySegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void DequeueReadOnlySegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.DequeueReadOnlySegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void NewMutableSegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.NewMutableSegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void NewDiskSegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.NewDiskSegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void EnqueueBottomSegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.EnqueueBottomSegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void DequeueBottomSegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.DequeueBottomSegment,
            SegmentId = segmentId
        };
        AppendRecord(record);
    }

    public void InsertBottomSegment(long segmentId, int index)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.InsertBottomSegment,
            SegmentId = segmentId,
            Index = index
        };
        AppendRecord(record);
    }

    public void DeleteBottomSegment(long segmentId)
    {
        var record = new MetaWalRecord
        {
            Operation = MetaWalOperation.DeleteBottomSegment,
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

    void AppendRecord(in MetaWalRecord record)
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
            throw new DataIsTooBigToLoadAtOnceException(len, int.MaxValue);
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
        long mutableSegment,
        long diskSegment,
        long[] readOnlySegments,
        long[] bottomSegments,
        bool createNew = false)
    {
        string productVersion = ZoneTreeInfo.ProductVersion.ToString();
        var newZoneTreeMeta = new ZoneTreeMeta
        {
            Version = productVersion,
            KeyType = zoneTreeMeta.KeyType,
            ValueType = zoneTreeMeta.ValueType,
            ComparerType = zoneTreeMeta.ComparerType,
            DiskSegment = diskSegment,
            ReadOnlySegments = readOnlySegments,
            MutableSegment = mutableSegment,
            KeySerializerType = zoneTreeMeta.KeySerializerType,
            ValueSerializerType = zoneTreeMeta.ValueSerializerType,
            WriteAheadLogOptions = zoneTreeMeta.WriteAheadLogOptions,
            DiskSegmentOptions = zoneTreeMeta.DiskSegmentOptions,
            MutableSegmentMaxItemCount = zoneTreeMeta.MutableSegmentMaxItemCount,
            DiskSegmentMaxItemCount = zoneTreeMeta.DiskSegmentMaxItemCount,
            BottomSegments = bottomSegments,
        };

        var bytes = JsonSerializeToUtf8Bytes(newZoneTreeMeta);
        var deviceManager = Options.RandomAccessDeviceManager;
        var metaFilePath = deviceManager.GetFilePath(0, MetaFileCategory);

        if (createNew)
        {
            using var stream = deviceManager.FileStreamProvider
                .CreateFileStream(
                    metaFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        }
        else
        {
            deviceManager
                .FileStreamProvider
                .GetDurableFileWriter()
                .WriteAllBytes(metaFilePath, bytes);
        }
        ClearContent();
        zoneTreeMeta.Version = productVersion;
        zoneTreeMeta.MutableSegment = mutableSegment;
        zoneTreeMeta.DiskSegment = diskSegment;
        zoneTreeMeta.ReadOnlySegments = readOnlySegments;
        zoneTreeMeta.BottomSegments = bottomSegments;
    }

    public static ZoneTreeMeta LoadZoneTreeMetaWithoutWALRecords(
        IRandomAccessDeviceManager deviceManager)
    {
        using var device = deviceManager
            .GetReadOnlyDevice(
                ZoneTreeMetaId,
                MetaFileCategory,
                isCompressed: false,
                compressionBlockSize: 0,
                MetaWALCompressionMethod,
                MetaWALCompressionLevel);

        if (device.Length > int.MaxValue)
            throw new DataIsTooBigToLoadAtOnceException(device.Length, int.MaxValue);
        var bytes = device.GetBytes(0, (int)device.Length);
        device.Close();
        deviceManager.RemoveReadOnlyDevice(device.SegmentId, MetaFileCategory);
        var meta = JsonDeserialize(bytes.Span);
        return meta;
    }

    private static byte[] JsonSerializeToUtf8Bytes(ZoneTreeMeta meta)
    {
#if NET6_0_OR_GREATER
        return JsonSerializer.SerializeToUtf8Bytes(
            meta,
            ZoneTreeMetaSourceGenerationContext.Default.ZoneTreeMeta);
#else
        return JsonSerializer.SerializeToUtf8Bytes(
                    meta,
                    new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    });
#endif
    }

    private static ZoneTreeMeta JsonDeserialize(byte[] bytes)
    {
#if NET6_0_OR_GREATER
        return JsonSerializer.Deserialize<ZoneTreeMeta>(bytes, ZoneTreeMetaSourceGenerationContext.Default.ZoneTreeMeta);
#else
        return JsonSerializer.Deserialize<ZoneTreeMeta>(bytes);
#endif
    }

    private static ZoneTreeMeta JsonDeserialize(Span<byte> bytes)
    {
#if NET6_0_OR_GREATER
        return JsonSerializer.Deserialize<ZoneTreeMeta>(bytes, ZoneTreeMetaSourceGenerationContext.Default.ZoneTreeMeta);
#else
        return JsonSerializer.Deserialize<ZoneTreeMeta>(bytes);
#endif
    }
}

#if NET6_0_OR_GREATER
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ZoneTreeMeta))]
internal partial class ZoneTreeMetaSourceGenerationContext : JsonSerializerContext
{
}
#endif

public enum MetaWalOperation
{
    EnqueueReadOnlySegment,
    DequeueReadOnlySegment,
    NewMutableSegment,
    NewDiskSegment,
    EnqueueBottomSegment,
    DequeueBottomSegment,
    InsertBottomSegment,
    DeleteBottomSegment,
}

[StructLayout(LayoutKind.Sequential)]
public struct MetaWalRecord : IEquatable<MetaWalRecord>
{
    public MetaWalOperation Operation;

    public long SegmentId;

    public int Index;

    public override bool Equals(object obj)
    {
        return obj is MetaWalRecord record && Equals(record);
    }

    public bool Equals(MetaWalRecord other)
    {
        return Operation == other.Operation &&
               SegmentId == other.SegmentId &&
               Index == other.Index;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Operation, SegmentId, Index);
    }

    public static bool operator ==(MetaWalRecord left, MetaWalRecord right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MetaWalRecord left, MetaWalRecord right)
    {
        return !(left == right);
    }
}