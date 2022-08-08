using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class MultiSectorDiskSegmentCreator<TKey, TValue> : IDiskSegmentCreator<TKey, TValue>
{
    readonly int SegmentId;

    readonly ISerializer<TKey> KeySerializer;

    readonly ISerializer<TValue> ValueSerializer;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly IIncrementalIdProvider IncrementalIdProvider;

    DiskSegmentCreator<TKey, TValue> NextCreator;

    readonly int DiskSegmentMaximumRecordCount;

    readonly List<IDiskSegment<TKey, TValue>> Sectors = new();

    readonly List<TKey> SectorKeys = new();

    readonly List<TValue> SectorValues = new();
        
    TKey LastAppendedKey;
    
    TValue LastAppendedValue;

    public HashSet<int> AppendedSectorSegmentIds { get; } = new();

    public int CurrentSectorLength => NextCreator.Length;

    public bool CanSkipCurrentSector =>
        NextCreator.Length == 0 || 
        NextCreator.Length >= Options.DiskSegmentMinimumRecordCount;

    public MultiSectorDiskSegmentCreator(
        ZoneTreeOptions<TKey, TValue> options,
        IIncrementalIdProvider incrementalIdProvider
        )
    {
        SegmentId = incrementalIdProvider.NextId();
        KeySerializer = options.KeySerializer;
        ValueSerializer = options.ValueSerializer;
        Options = options;
        IncrementalIdProvider = incrementalIdProvider;
        NextCreator = new(options, incrementalIdProvider);
        DiskSegmentMaximumRecordCount = Options.DiskSegmentMaximumRecordCount;
    }

    public void Append(TKey key, TValue value)
    {
        var len = NextCreator.Length;
        if (len == 0) {
            SectorKeys.Add(key);
            SectorValues.Add(value);
        }
        else if (len == DiskSegmentMaximumRecordCount - 1)
        {
            SectorKeys.Add(key);
            SectorValues.Add(value);
            NextCreator.Append(key, value);
            var sector = NextCreator.CreateReadOnlyDiskSegment();
            Sectors.Add(sector);
            NextCreator = new (Options, IncrementalIdProvider);
            return;
        }
        NextCreator.Append(key, value);
        LastAppendedKey = key;
        LastAppendedValue = value;
    }

    public void Append(
        IDiskSegment<TKey, TValue> sector,
        TKey key1,
        TKey key2,
        TValue value1,
        TValue value2)
    {
        if (NextCreator.Length > 0)
        {
            SectorKeys.Add(LastAppendedKey);
            SectorValues.Add(LastAppendedValue);
            var currentSector = NextCreator.CreateReadOnlyDiskSegment();
            Sectors.Add(currentSector);
            NextCreator = new(Options, IncrementalIdProvider);
        }
        AppendedSectorSegmentIds.Add(sector.SegmentId);
        Sectors.Add(sector);
        SectorKeys.Add(key1);
        SectorKeys.Add(key2);
        SectorValues.Add(value1);
        SectorValues.Add(value2);
    }

    public IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment()
    {
        if (NextCreator.Length == 0)
        {
            NextCreator.DropDiskSegment();
        }
        else
        {
            SectorKeys.Add(LastAppendedKey);
            SectorValues.Add(LastAppendedValue);
            var sector = NextCreator.CreateReadOnlyDiskSegment();
            Sectors.Add(sector);
        }

        WriteMultiDiskSegment();

        var diskSegment = new MultiSectorDiskSegment<TKey, TValue>(
            SegmentId, 
            Options,
            Sectors,
            SectorKeys.ToArray(),
            SectorValues.ToArray());
        return diskSegment;
    }

    private void WriteMultiDiskSegment()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        WriteSectors(bw);
        WriteKeys(bw);
        WriteValues(bw);
        bw.Flush();

        using var multiDevice = Options.RandomAccessDeviceManager.CreateWritableDevice(
                    SegmentId,
                    DiskSegmentConstants.MultiSectorDiskSegmentCategory,
                    false,
                    0,
                    0);
        var compressedBytes = DataCompression.Compress(ms.ToArray());
        multiDevice.AppendBytesReturnPosition(compressedBytes);
        Options.RandomAccessDeviceManager
            .RemoveWritableDevice(SegmentId, DiskSegmentConstants.MultiSectorDiskSegmentCategory);
    }

    void WriteSectors(BinaryWriter bw)
    {
        var len = Sectors.Count;
        bw.Write(len);
        for (var i = 0; i < len; ++i)
            bw.Write(Sectors[i].SegmentId);        
    }

    void WriteKeys(BinaryWriter bw)
    {
        var len = Sectors.Count * 2;
        bw.Write(len);
        for (var i = 0; i < len; ++i)
        {
            var a = i++;
            var b = i;
            var k1 = SectorKeys[a];
            var bytes = KeySerializer.Serialize(k1);
            bw.Write(bytes.Length);
            bw.Write(bytes);

            var k2 = SectorKeys[b];
            bytes = KeySerializer.Serialize(k2);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }
    }

    void WriteValues(BinaryWriter bw)
    {
        var len = Sectors.Count * 2;
        bw.Write(len);
        for (var i = 0; i < len; ++i)
        {
            var a = i++;
            var b = i;
            var v1 = SectorValues[a];
            var bytes = ValueSerializer.Serialize(v1);
            bw.Write(bytes.Length);
            bw.Write(bytes);

            var v2 = SectorValues[b];
            bytes = ValueSerializer.Serialize(v2);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }
    }

    public void DropDiskSegment()
    {
        foreach(var sector in Sectors)
        {
            if (AppendedSectorSegmentIds.Contains(sector.SegmentId))
                continue;
            sector.Drop();
        }
        using var multiDevice = Options.RandomAccessDeviceManager
            .GetReadOnlyDevice(SegmentId, DiskSegmentConstants.MultiSectorDiskSegmentCategory,
            false, 0, 0);
        multiDevice.Delete();
        Options.RandomAccessDeviceManager
            .RemoveReadOnlyDevice(SegmentId, DiskSegmentConstants.MultiSectorDiskSegmentCategory);
    }

    public void Dispose()
    {
    }
}