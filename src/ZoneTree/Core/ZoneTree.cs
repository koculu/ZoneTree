using System.Collections.Concurrent;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public const string SegmentWalCategory = "seg";

    public ILogger Logger { get; }

    readonly ZoneTreeMeta ZoneTreeMeta = new();

    readonly ZoneTreeMetaWAL<TKey, TValue> MetaWal;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly MinHeapEntryRefComparer<TKey, TValue> MinHeapEntryComparer;

    readonly MaxHeapEntryRefComparer<TKey, TValue> MaxHeapEntryComparer;

    readonly IsValueDeletedDelegate<TValue> IsValueDeleted;

    readonly SingleProducerSingleConsumerQueue<IReadOnlySegment<TKey, TValue>> ReadOnlySegmentQueue = new();

    volatile SingleProducerSingleConsumerQueue<IDiskSegment<TKey, TValue>> BottomSegmentQueue = new();

    readonly IIncrementalIdProvider IncrementalIdProvider = new IncrementalIdProvider();

    readonly object AtomicUpdateLock = new();

    readonly object LongMergerLock = new();

    readonly object LongBottomSegmentsMergerLock = new();

    readonly object ShortMergerLock = new();

    volatile bool IsMergingFlag;

    volatile bool IsBottomSegmentsMergingFlag;

    volatile bool IsCancelMergeRequested = false;

    volatile bool IsCancelBottomSegmentsMergeRequested = false;

    volatile IMutableSegment<TKey, TValue> _mutableSegment;

    public IMutableSegment<TKey, TValue> MutableSegment { get => _mutableSegment; private set => _mutableSegment = value; }

    public IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments =>
        ReadOnlySegmentQueue.ToLastInFirstArray();

    volatile IDiskSegment<TKey, TValue> _diskSegment = new NullDiskSegment<TKey, TValue>();

    public IDiskSegment<TKey, TValue> DiskSegment { get => _diskSegment; private set => _diskSegment = value; }

    public IReadOnlyList<IDiskSegment<TKey, TValue>> BottomSegments =>
        BottomSegmentQueue.ToLastInFirstArray();

    public bool IsMerging { get => IsMergingFlag; private set => IsMergingFlag = value; }

    public bool IsBottomSegmentsMerging { get => IsBottomSegmentsMergingFlag; private set => IsBottomSegmentsMergingFlag = value; }

    public int ReadOnlySegmentsCount => ReadOnlySegmentQueue.Length;

    public long ReadOnlySegmentsRecordCount => ReadOnlySegmentQueue.Sum(x => x.Length);

    public long MutableSegmentRecordCount => MutableSegment.Length;

    public long InMemoryRecordCount
    {
        get
        {
            lock (AtomicUpdateLock)
            {
                return MutableSegment.Length + ReadOnlySegmentsRecordCount;
            }
        }
    }

    public long TotalRecordCount
    {
        get
        {
            lock (ShortMergerLock)
            {
                return InMemoryRecordCount +
                    DiskSegment.Length +
                    BottomSegmentQueue.Sum(x => x.Length);
            }
        }
    }

    public IZoneTreeMaintenance<TKey, TValue> Maintenance => this;

    public event MutableSegmentMovedForward<TKey, TValue> OnMutableSegmentMovedForward;

    public event MergeOperationStarted<TKey, TValue> OnMergeOperationStarted;

    public event MergeOperationEnded<TKey, TValue> OnMergeOperationEnded;

    public event BottomSegmentsMergeOperationStarted<TKey, TValue> OnBottomSegmentsMergeOperationStarted;

    public event BottomSegmentsMergeOperationEnded<TKey, TValue> OnBottomSegmentsMergeOperationEnded;

    public event DiskSegmentCreated<TKey, TValue> OnDiskSegmentCreated;

    public event DiskSegmentCreated<TKey, TValue> OnDiskSegmentActivated;

    public event CanNotDropReadOnlySegment<TKey, TValue> OnCanNotDropReadOnlySegment;

    public event CanNotDropDiskSegment<TKey, TValue> OnCanNotDropDiskSegment;

    public event CanNotDropDiskSegmentCreator<TKey, TValue> OnCanNotDropDiskSegmentCreator;

    public event ZoneTreeIsDisposing<TKey, TValue> OnZoneTreeIsDisposing;

    volatile bool _isReadOnly;

    public bool IsReadOnly { get => _isReadOnly; set => _isReadOnly = value; }

    public ZoneTree(ZoneTreeOptions<TKey, TValue> options)
    {
        Logger = options.Logger;
        options.WriteAheadLogProvider.InitCategory(SegmentWalCategory);
        MetaWal = new ZoneTreeMetaWAL<TKey, TValue>(options, false);
        Options = options;
        MinHeapEntryComparer = new MinHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        MaxHeapEntryComparer = new MaxHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        MutableSegment = new MutableSegment<TKey, TValue>(
            options, IncrementalIdProvider.NextId(), new IncrementalIdProvider());
        IsValueDeleted = options.IsValueDeleted;
        FillZoneTreeMeta();
        MetaWal.SaveMetaData(
            ZoneTreeMeta,
            MutableSegment.SegmentId,
            DiskSegment.SegmentId,
            Array.Empty<long>(),
            Array.Empty<long>(),
            true);
    }

    public ZoneTree(
        ZoneTreeOptions<TKey, TValue> options,
        ZoneTreeMeta meta,
        IReadOnlyList<IReadOnlySegment<TKey, TValue>> readOnlySegments,
        IMutableSegment<TKey, TValue> mutableSegment,
        IDiskSegment<TKey, TValue> diskSegment,
        IReadOnlyList<IDiskSegment<TKey, TValue>> bottomSegments,
        long maximumSegmentId
        )
    {
        Logger = options.Logger;
        options.WriteAheadLogProvider.InitCategory(SegmentWalCategory);
        IncrementalIdProvider.SetNextId(maximumSegmentId + 1);
        MetaWal = new ZoneTreeMetaWAL<TKey, TValue>(options, false);
        ZoneTreeMeta = meta;
        Options = options;
        MinHeapEntryComparer = new MinHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        MaxHeapEntryComparer = new MaxHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        MutableSegment = mutableSegment;
        DiskSegment = diskSegment;
        DiskSegment.DropFailureReporter = (ds, e) => ReportDropFailure(ds, e);
        foreach (var ros in readOnlySegments.Reverse())
            ReadOnlySegmentQueue.Enqueue(ros);
        foreach (var bs in bottomSegments.Reverse())
            BottomSegmentQueue.Enqueue(bs);
        IsValueDeleted = options.IsValueDeleted;
    }

    void FillZoneTreeMeta()
    {
        if (MutableSegment != null)
            ZoneTreeMeta.MutableSegment = MutableSegment.SegmentId;
        ZoneTreeMeta.ComparerType = Options.Comparer.GetType().FullName;
        ZoneTreeMeta.KeyType = typeof(TKey).FullName;
        ZoneTreeMeta.ValueType = typeof(TValue).FullName;
        ZoneTreeMeta.KeySerializerType = Options.KeySerializer.GetType().FullName;
        ZoneTreeMeta.ValueSerializerType = Options.ValueSerializer.GetType().FullName;
        ZoneTreeMeta.DiskSegment = DiskSegment.SegmentId;
        ZoneTreeMeta.ReadOnlySegments = ReadOnlySegmentQueue.Select(x => x.SegmentId).ToArray();
        ZoneTreeMeta.BottomSegments = BottomSegmentQueue.Select(x => x.SegmentId).ToArray();
        ZoneTreeMeta.MutableSegmentMaxItemCount = Options.MutableSegmentMaxItemCount;
        ZoneTreeMeta.DiskSegmentMaxItemCount = Options.DiskSegmentMaxItemCount;
        ZoneTreeMeta.WriteAheadLogOptions = Options.WriteAheadLogOptions;
        ZoneTreeMeta.DiskSegmentOptions = Options.DiskSegmentOptions;
    }

    void ReportDropFailure(IDiskSegment<TKey, TValue> ds, Exception e)
    {
        OnCanNotDropDiskSegment?.Invoke(ds, e);
    }

    public void SaveMetaData()
    {
        lock (ShortMergerLock)
            lock (AtomicUpdateLock)
            {
                MetaWal.SaveMetaData(
                    ZoneTreeMeta,
                    MutableSegment.SegmentId,
                    DiskSegment.SegmentId,
                    ReadOnlySegmentQueue.Select(x => x.SegmentId).ToArray(),
                    BottomSegmentQueue.Select(x => x.SegmentId).ToArray());
            }
    }

    public void Dispose()
    {
        OnZoneTreeIsDisposing?.Invoke(this);
        MutableSegment.ReleaseResources();
        DiskSegment.Dispose();
        MetaWal.Dispose();
        foreach (var ros in ReadOnlySegments)
            ros.ReleaseResources();
        foreach (var bs in BottomSegmentQueue)
            bs.ReleaseResources();
    }

    public void DestroyTree()
    {
        MetaWal.Dispose();
        MutableSegment.Drop();
        DiskSegment.Drop();
        DiskSegment.Dispose();
        foreach (var ros in ReadOnlySegmentQueue)
            ros.Drop();
        foreach (var bs in BottomSegmentQueue)
            bs.Drop();
        Options.WriteAheadLogProvider.DropStore();
        Options.RandomAccessDeviceManager.DropStore();
    }

    public long Count()
    {
        using var iterator = CreateInMemorySegmentsIterator(
            autoRefresh: false,
            includeDeletedRecords: true);

        IDiskSegment<TKey, TValue> diskSegment = null;
        lock (ShortMergerLock)
            lock (AtomicUpdateLock)
            {
                // 2 things to synchronize with
                // MoveSegmentForward and disk merger segment swap.
                diskSegment = DiskSegment;
                iterator.Refresh();
            }

        if (!BottomSegmentQueue.IsEmpty)
            return CountFullScan();

        var count = diskSegment.Length;
        while (iterator.Next())
        {
            var hasKey = diskSegment.ContainsKey(iterator.CurrentKey);
            var isValueDeleted = IsValueDeleted(iterator.CurrentValue);
            if (hasKey)
            {
                if (isValueDeleted)
                    --count;
            }
            else
            {
                if (!isValueDeleted)
                    ++count;
            }
        }
        return count;
    }

    public long CountFullScan()
    {
        using var iterator = CreateIterator(IteratorType.NoRefresh, false);
        var count = 0;
        while (iterator.Next())
            ++count;
        return count;
    }

    public IMaintainer CreateMaintainer()
    {
        return new ZoneTreeMaintainer<TKey, TValue>(this);
    }
}
