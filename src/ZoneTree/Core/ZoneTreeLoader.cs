using Tenray.ZoneTree.Exceptions;
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

    int maximumSegmentId = 1;
    public ZoneTreeLoader(ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
        Options.Validate();
    }

    public bool ZoneTreeMetaExists => ZoneTreeMetaWAL<TKey, TValue>.Exists(Options);

    void LoadZoneTreeMeta()
    {
        ZoneTreeMeta = ZoneTreeMetaWAL<TKey, TValue>.LoadZoneTreeMetaWithoutWALRecords(Options);
        SetMaximumSegmentId(ZoneTreeMeta.SegmentZero);
        SetMaximumSegmentId(ZoneTreeMeta.DiskSegment);
        SetMaximumSegmentId(ZoneTreeMeta.ReadOnlySegments.FirstOrDefault());
        ValidateZoneTreeMeta();
    }

    private void SetMaximumSegmentId(int newId)
    {
        maximumSegmentId = Math.Max(maximumSegmentId, newId);
    }

    private void ValidateZoneTreeMeta()
    {
        if (!string.Equals(ZoneTreeMeta.KeyType, typeof(TKey).FullName))
            throw new TreeKeyTypeMismatchException(
                typeof(TKey).FullName,
                ZoneTreeMeta.KeyType);

        if (!string.Equals(ZoneTreeMeta.ValueType, typeof(TKey).FullName))
            throw new TreeValueTypeMismatchException(
                typeof(TValue).FullName,
                ZoneTreeMeta.ValueType);

        if (!string.Equals(ZoneTreeMeta.ComparerType, Options.Comparer.GetType().FullName))
            throw new TreeComparerMismatchException(
                Options.Comparer.GetType().FullName,
                ZoneTreeMeta.ComparerType);

        if (!string.Equals(ZoneTreeMeta.KeySerializerType, Options.KeySerializer.GetType().FullName))
            throw new TreeKeySerializerTypeMismatchException(
                Options.KeySerializer.GetType().FullName,
                ZoneTreeMeta.KeySerializerType);

        if (!string.Equals(ZoneTreeMeta.ValueSerializerType, Options.ValueSerializer.GetType().FullName))
            throw new TreeValueSerializerTypeMismatchException(
                Options.KeySerializer.GetType().FullName,
                ZoneTreeMeta.KeySerializerType);
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

    private void ValidateSegmentOrder()
    {
        var index = int.MaxValue;
        foreach (var ros in ZoneTreeMeta.ReadOnlySegments)
        {
            if (index <= ros)
                throw new ZoneTreeMetaCorruptionException();
            index = ros;
        }
    }

    void LoadReadOnlySegments()
    {
        var segments = ZoneTreeMeta.ReadOnlySegments;
        var loader = new ReadOnlySegmentLoader<TKey, TValue>(Options);
        var list = new List<IReadOnlySegment<TKey, TValue>>();
        foreach (var segment in segments)
        {
            var ros = loader.LoadReadOnlySegment(segment);
            list.Add(ros);
        }
        ReadOnlySegments = list;
    }

    void LoadDiskSegment()
    {
        var segmentId = ZoneTreeMeta.DiskSegment;
        DiskSegment = segmentId == 0
            ? new NullDiskSegment<TKey, TValue>()
            : new DiskSegment<TKey, TValue>(ZoneTreeMeta.DiskSegment, Options);
    }

    void LoadSegmentZero()
    {
        var loader = new MutableSegmentLoader<TKey, TValue>(Options);
        SegmentZero = loader.LoadMutableSegment(ZoneTreeMeta.SegmentZero);
    }

    public ZoneTree<TKey, TValue> LoadZoneTree()
    {
        LoadZoneTreeMeta();
        LoadZoneTreeMetaWAL();
        LoadSegmentZero();
        LoadDiskSegment();
        LoadReadOnlySegments();
        var zoneTree = new ZoneTree<TKey, TValue>(Options, ZoneTreeMeta,
            ReadOnlySegments, SegmentZero, DiskSegment, maximumSegmentId);
        return zoneTree;
    }

    // TODO: implement the meta file recovery from existing files
    private ZoneTree<TKey, TValue> RecoverMetaFileAndLoadZoneTree()
    {
        RecoverZoneTreeMeta();
        LoadSegmentZero();
        LoadDiskSegment();
        LoadReadOnlySegments();
        var zoneTree = new ZoneTree<TKey, TValue>(Options, ZoneTreeMeta,
            ReadOnlySegments, SegmentZero, DiskSegment, maximumSegmentId);
        return zoneTree;
    }

    private void RecoverZoneTreeMeta()
    {
    }
}