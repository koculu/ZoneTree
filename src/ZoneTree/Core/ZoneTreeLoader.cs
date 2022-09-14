using System.Collections.Concurrent;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTreeLoader<TKey, TValue>
{
    ZoneTreeOptions<TKey, TValue> Options { get; }

    ZoneTreeMeta ZoneTreeMeta;

    IMutableSegment<TKey, TValue> MutableSegment { get; set; }

    IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments;

    IDiskSegment<TKey, TValue> DiskSegment { get; set; }

    IReadOnlyList<IDiskSegment<TKey, TValue>> BottomSegments;

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
            Version.Parse(version) < new Version("1.4.9"))
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
        var bottomSegments = ZoneTreeMeta.BottomSegments?.ToList() ?? new List<long>();
        foreach (var record in records)
        {
            var segmentId = record.SegmentId;
            SetMaximumSegmentId(segmentId);
            switch (record.Operation)
            {
                case MetaWalOperation.NewMutableSegment:
                    ZoneTreeMeta.MutableSegment = segmentId;
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
                case MetaWalOperation.EnqueueBottomSegment:
                    bottomSegments.Insert(0, segmentId);
                    break;
                case MetaWalOperation.DequeueBottomSegment:
                    if (bottomSegments.Count == 0 ||
                        bottomSegments.Last() != segmentId)
                        throw new WriteAheadLogCorruptionException(segmentId, null);
                    bottomSegments.RemoveAt(bottomSegments.Count - 1);
                    break;
                case MetaWalOperation.InsertBottomSegment:
                    bottomSegments.Insert(record.Index, segmentId);
                    break;
                case MetaWalOperation.DeleteBottomSegment:
                    bottomSegments.Remove(segmentId);
                    break;
            }
        }
        ZoneTreeMeta.ReadOnlySegments = readOnlySegments;
        ZoneTreeMeta.BottomSegments = bottomSegments;
        metaWal.SaveMetaData(
            ZoneTreeMeta,
            ZoneTreeMeta.MutableSegment,
            ZoneTreeMeta.DiskSegment,
            readOnlySegments.ToArray(),
            bottomSegments.ToArray());
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

    void LoadMutableSegment(long maximumOpIndex)
    {
        var loader = new MutableSegmentLoader<TKey, TValue>(Options);
        MutableSegment = loader
            .LoadMutableSegment(ZoneTreeMeta.MutableSegment, maximumOpIndex);
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
            DiskSegment = new MultiPartDiskSegment<TKey, TValue>(segmentId, Options);
            return;
        }
        DiskSegment = new DiskSegment<TKey, TValue>(segmentId, Options);
    }

    void LoadBottomSegments()
    {
        var segments = ZoneTreeMeta.BottomSegments;
        var map = new ConcurrentDictionary<long, IDiskSegment<TKey, TValue>>();

        Parallel.ForEach(segments, (segmentId) =>
        {
            if (Options.RandomAccessDeviceManager
                .DeviceExists(segmentId,
                    DiskSegmentConstants.MultiPartDiskSegmentCategory))
            {
                var ds = new MultiPartDiskSegment<TKey, TValue>(segmentId, Options);
                map.AddOrUpdate(segmentId, ds, (_, _) => ds);
            }
            else
            {
                var ds = new DiskSegment<TKey, TValue>(segmentId, Options);
                map.AddOrUpdate(segmentId, ds, (_, _) => ds);
            }
        });
        BottomSegments = segments.Select(x => map[x]).ToArray();
    }

    void SetMaximumId()
    {
        SetMaximumSegmentId(ZoneTreeMeta.MutableSegment);
        SetMaximumSegmentId(ZoneTreeMeta.DiskSegment);
        SetMaximumSegmentId(MultiPartDiskSegment<TKey, TValue>
            .ReadMaximumSegmentId(ZoneTreeMeta.DiskSegment, Options.RandomAccessDeviceManager));
        var ros = ZoneTreeMeta.ReadOnlySegments;
        var maximumId = ros.Count > 0 ? ros[0] : 0;
        SetMaximumSegmentId(maximumId);
        var bs = ZoneTreeMeta.BottomSegments;
        maximumId = bs.Count > 0 ? bs.Max() : 0;
        SetMaximumSegmentId(maximumId);
    }
    public ZoneTree<TKey, TValue> LoadZoneTree()
    {
        LoadZoneTreeMeta();
        LoadZoneTreeMetaWAL();
        SetMaximumId();
        var maximumOpIndex = LoadReadOnlySegments();
        LoadMutableSegment(maximumOpIndex);
        LoadDiskSegment();
        LoadBottomSegments();
        var zoneTree = new ZoneTree<TKey, TValue>(Options, ZoneTreeMeta,
            ReadOnlySegments, MutableSegment, DiskSegment, BottomSegments, maximumSegmentId);
        return zoneTree;
    }
}