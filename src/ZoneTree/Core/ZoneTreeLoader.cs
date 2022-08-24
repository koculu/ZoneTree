using System.Collections.Concurrent;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public class ZoneTreeLoader<TKey, TValue>
{
    ZoneTreeOptions<TKey, TValue> Options { get; }

    ZoneTreeMeta ZoneTreeMeta;

    IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments;

    IDiskSegment<TKey, TValue> DiskSegment { get; set; }

    IMutableSegment<TKey, TValue> SegmentZero { get; set; }

    long maximumSegmentId = 1;
    public ZoneTreeLoader(ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
        Options.Validate();
    }

    public bool ZoneTreeMetaExists => ZoneTreeMetaWAL<TKey, TValue>.Exists(Options);

    void LoadZoneTreeMeta()
    {
        ZoneTreeMeta = ZoneTreeMetaWAL<TKey, TValue>
            .LoadZoneTreeMetaWithoutWALRecords(Options.RandomAccessDeviceManager);        
        ValidateZoneTreeMeta();
    }

    void SetMaximumSegmentId(long newId)
    {
        maximumSegmentId = Math.Max(maximumSegmentId, newId);
    }

    void ValidateZoneTreeMeta()
    {
        var version = ZoneTreeMeta.Version;
        if (string.IsNullOrWhiteSpace(version) ||
            Version.Parse(version) < new Version("1.3.8"))
            throw new ExistingDatabaseVersionIsNotCompatibleException(
                Version.Parse(version),
                ZoneTreeInfo.ProductVersion);

        if (!string.Equals(ZoneTreeMeta.KeyType, typeof(TKey).FullName))
            throw new TreeKeyTypeMismatchException(
                ZoneTreeMeta.KeyType,
                typeof(TKey).FullName);

        if (!string.Equals(ZoneTreeMeta.ValueType, typeof(TValue).FullName))
            throw new TreeValueTypeMismatchException(                
                ZoneTreeMeta.ValueType,
                typeof(TValue).FullName);

        if (!string.Equals(ZoneTreeMeta.ComparerType, Options.Comparer.GetType().FullName))
            throw new TreeComparerMismatchException(
                ZoneTreeMeta.ComparerType,
                Options.Comparer.GetType().FullName);

        if (!string.Equals(ZoneTreeMeta.KeySerializerType, Options.KeySerializer.GetType().FullName))
            throw new TreeKeySerializerTypeMismatchException(                
                ZoneTreeMeta.KeySerializerType,
                Options.KeySerializer.GetType().FullName);

        if (!string.Equals(ZoneTreeMeta.ValueSerializerType, Options.ValueSerializer.GetType().FullName))
            throw new TreeValueSerializerTypeMismatchException(
                ZoneTreeMeta.KeySerializerType,
                Options.KeySerializer.GetType().FullName);
    }

    void LoadZoneTreeMetaWAL()
    {
        using var metaWal = new ZoneTreeMetaWAL<TKey, TValue>(Options, false);
        var records = metaWal.GetAllRecords();
        var readOnlySegments = ZoneTreeMeta.ReadOnlySegments.ToList();
        foreach (var record in records)
        {
            var segmentId = record.SegmentId;
            SetMaximumSegmentId(segmentId);
            switch (record.Operation)
            {
                case MetaWalOperation.NewSegmentZero:
                    ZoneTreeMeta.SegmentZero = segmentId;
                    break;
                case MetaWalOperation.NewDiskSegment:
                    ZoneTreeMeta.DiskSegment = segmentId;
                    break;
                case MetaWalOperation.EnqueueReadOnlySegment:
                    readOnlySegments.Insert(0, segmentId);
                    break;
                case MetaWalOperation.DequeueReadOnlySegment:
                    if (readOnlySegments.Count == 0 ||
                        readOnlySegments.Last() != segmentId)
                        throw new WriteAheadLogCorruptionException(segmentId, null);
                    readOnlySegments.RemoveAt(readOnlySegments.Count - 1);
                    break;
            }
        }
        ZoneTreeMeta.ReadOnlySegments = readOnlySegments;
        metaWal.SaveMetaData(
            ZoneTreeMeta,
            ZoneTreeMeta.SegmentZero,
            ZoneTreeMeta.DiskSegment,
            readOnlySegments.ToArray());
        ValidateSegmentOrder();
    }

    void ValidateSegmentOrder()
    {
        var index = long.MaxValue;
        foreach (var ros in ZoneTreeMeta.ReadOnlySegments)
        {
            if (index <= ros)
                throw new ZoneTreeMetaCorruptionException();
            index = ros;
        }
    }

    long LoadReadOnlySegments()
    {
        long maximumOpIndex = 0;
        var segments = ZoneTreeMeta.ReadOnlySegments;
        var map = new ConcurrentDictionary<long, IReadOnlySegment<TKey, TValue>>();

        var loader = new ReadOnlySegmentLoader<TKey, TValue>(Options); 
        Parallel.ForEach(segments, (segment) =>
        {
            var ros = loader.LoadReadOnlySegment(segment);            
            map.TryAdd(segment, ros);
        });
        foreach (var ros in map.Values)
        {
            maximumOpIndex = Math.Max(ros.MaximumOpIndex, maximumOpIndex);
        }
        ReadOnlySegments = segments.Select(x => map[x]).ToArray();
        return maximumOpIndex;
    }

    void LoadDiskSegment()
    {
        var segmentId = ZoneTreeMeta.DiskSegment;
        if (segmentId == 0)
        {
            DiskSegment = new NullDiskSegment<TKey, TValue>();
            return;
        }
        if (Options.RandomAccessDeviceManager
            .DeviceExists(segmentId, DiskSegmentConstants.MultiPartDiskSegmentCategory))
        {
            DiskSegment = new MultiPartDiskSegment<TKey, TValue>(ZoneTreeMeta.DiskSegment, Options);
            return;
        }
        DiskSegment = new DiskSegment<TKey, TValue>(ZoneTreeMeta.DiskSegment, Options);
    }

    void LoadSegmentZero(long maximumOpIndex)
    {
        var loader = new MutableSegmentLoader<TKey, TValue>(Options);
        SegmentZero = loader
            .LoadMutableSegment(ZoneTreeMeta.SegmentZero, maximumOpIndex);
    }

    void SetMaximumId()
    {
        SetMaximumSegmentId(ZoneTreeMeta.SegmentZero);
        SetMaximumSegmentId(ZoneTreeMeta.DiskSegment);
        SetMaximumSegmentId(MultiPartDiskSegment<TKey, TValue>
            .ReadMaximumSegmentId(ZoneTreeMeta.DiskSegment, Options.RandomAccessDeviceManager));
        var ros = ZoneTreeMeta.ReadOnlySegments;
        var maximumId = ros.Count > 0 ? ros[0] : 0;
        SetMaximumSegmentId(maximumId);
    }
    public ZoneTree<TKey, TValue> LoadZoneTree()
    {
        LoadZoneTreeMeta();
        LoadZoneTreeMetaWAL();
        SetMaximumId();
        var maximumOpIndex = LoadReadOnlySegments();
        LoadSegmentZero(maximumOpIndex);
        LoadDiskSegment();
        var zoneTree = new ZoneTree<TKey, TValue>(Options, ZoneTreeMeta,
            ReadOnlySegments, SegmentZero, DiskSegment, maximumSegmentId);
        return zoneTree;
    }
}