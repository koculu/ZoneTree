using System.Collections.Concurrent;
using Tenray;
using Tenray.Collections;
using Tenray.Segments;
using ZoneTree.Collections;
using ZoneTree.Segments.Disk;

namespace ZoneTree.Core;

public sealed class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    readonly ZoneTreeMeta ZoneTreeMeta = new();

    readonly ZoneTreeMetaWAL<TKey, TValue> MetaWal;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly MinHeapEntryRefComparer<TKey, TValue> MinHeapEntryComparer;

    readonly MaxHeapEntryRefComparer<TKey, TValue> MaxHeapEntryComparer;

    readonly IsValueDeletedDelegate<TValue> IsValueDeleted;

    readonly ConcurrentQueue<IReadOnlySegment<TKey, TValue>> ReadOnlySegmentQueue = new();

    readonly IIncrementalIdProvider IncrementalIdProvider = new IncrementalIdProvider();

    readonly object AtomicUpdateLock = new();

    readonly object LongMergerLock = new();

    readonly object ShortMergerLock = new();

    volatile bool IsMergingFlag;

    volatile bool IsCancelMergeRequested = false;

    public IMutableSegment<TKey, TValue> SegmentZero { get; private set; }

    public IDiskSegment<TKey, TValue> DiskSegment { get; private set; } = new NullDiskSegment<TKey, TValue>();

    public IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments =>
        ReadOnlySegmentQueue.ToArray();

    public bool IsMerging { get => IsMergingFlag; private set => IsMergingFlag = value; }

    public int ReadOnlySegmentsCount => ReadOnlySegmentQueue.Count;

    public int ReadOnlySegmentsRecordCount => ReadOnlySegmentQueue.Sum(x => x.Length);

    public int InMemoryRecordCount => 
        SegmentZero.Length + ReadOnlySegmentsRecordCount;

    public int TotalRecordCount => InMemoryRecordCount + DiskSegment.Length;

    public IZoneTreeMaintenance<TKey, TValue> Maintenance => this;

    public event SegmentZeroMovedForward<TKey, TValue> OnSegmentZeroMovedForward;

    public event MergeOperationStarted<TKey, TValue> OnMergeOperationStarted;

    public event MergeOperationEnded<TKey, TValue> OnMergeOperationEnded;

    public event DiskSegmentCreated<TKey, TValue> OnDiskSegmentCreated;
    
    public event DiskSegmentCreated<TKey, TValue> OnDiskSegmentActivated;

    public event CanNotDropReadOnlySegment<TKey, TValue> OnCanNotDropReadOnlySegment;

    public event CanNotDropDiskSegment<TKey, TValue> OnCanNotDropDiskSegment;

    public event CanNotDropDiskSegmentCreator<TKey, TValue> OnCanNotDropDiskSegmentCreator;

    public event ZoneTreeIsDisposing<TKey, TValue> OnZoneTreeIsDisposing;

    public ZoneTree(ZoneTreeOptions<TKey, TValue> options)
    {
        MetaWal = new ZoneTreeMetaWAL<TKey, TValue>(options, false);
        Options = options;
        MinHeapEntryComparer = new MinHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        MaxHeapEntryComparer = new MaxHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        SegmentZero = new MutableSegment<TKey, TValue>(options, IncrementalIdProvider.NextId());
        IsValueDeleted = options.IsValueDeleted;
        FillZoneTreeMeta();
        MetaWal.SaveMetaData(
            ZoneTreeMeta,
            SegmentZero.SegmentId,
            DiskSegment.SegmentId,
            Array.Empty<int>());
    }

    public ZoneTree(
        ZoneTreeOptions<TKey, TValue> options,
        ZoneTreeMeta meta,
        IReadOnlyList<IReadOnlySegment<TKey, TValue>> readOnlySegments,
        IMutableSegment<TKey, TValue> segmentZero,
        IDiskSegment<TKey, TValue> diskSegment,
        int maximumSegmentId
        )
    {
        IncrementalIdProvider.SetNextId(maximumSegmentId + 1);
        MetaWal = new ZoneTreeMetaWAL<TKey, TValue>(options, false);
        ZoneTreeMeta = meta;
        Options = options;
        MinHeapEntryComparer = new MinHeapEntryRefComparer<TKey, TValue>(options.Comparer);
        SegmentZero = segmentZero;
        DiskSegment = diskSegment;
        DiskSegment.DropFailureReporter = (ds, e) => ReportDropFailure(ds, e);
        foreach (var ros in readOnlySegments.Reverse())
            ReadOnlySegmentQueue.Enqueue(ros);
        IsValueDeleted = options.IsValueDeleted;
    }

    private void FillZoneTreeMeta()
    {
        if (SegmentZero != null)
            ZoneTreeMeta.SegmentZero = SegmentZero.SegmentId;
        ZoneTreeMeta.ComparerType = Options.Comparer.GetType().FullName;
        ZoneTreeMeta.KeyType = typeof(TKey).FullName;
        ZoneTreeMeta.ValueType = typeof(TValue).FullName;
        ZoneTreeMeta.KeySerializerType = Options.KeySerializer.GetType().FullName;
        ZoneTreeMeta.ValueSerializerType = Options.ValueSerializer.GetType().FullName;
        ZoneTreeMeta.DiskSegment = DiskSegment.SegmentId;
        ZoneTreeMeta.ReadOnlySegments = ReadOnlySegmentQueue.Select(x => x.SegmentId).Reverse().ToArray();
    }

    public bool ContainsKey(in TKey key)
    {
        TValue value;
        if (SegmentZero.ContainsKey(key))
        {
            if (SegmentZero.TryGet(key, out value))
                return !IsValueDeleted(value);
        }

        foreach (var segment in ReadOnlySegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }

        if (DiskSegment.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }
        return false;
    }

    private bool TryGetFromReadonlySegments(in TKey key, out TValue value)
    {
        foreach (var segment in ReadOnlySegmentQueue.Reverse())
        {
            if (segment.TryGet(key, out value))
            {
                return !IsValueDeleted(value);
            }
        }

        if (DiskSegment.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }
        return false;
    }

    public bool TryGet(in TKey key, out TValue value)
    {
        if (SegmentZero.TryGet(key, out value))
        {
            return !IsValueDeleted(value);
        }
        return TryGetFromReadonlySegments(in key, out value);
    }

    public bool TryAtomicAdd(in TKey key, in TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        lock (AtomicUpdateLock)
        {
            if (ContainsKey(key))
                return false;
            Upsert(key, value);
            return true;
        }
    }

    public bool TryAtomicUpdate(in TKey key, in TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        lock (AtomicUpdateLock)
        {
            if (!ContainsKey(key))
                return false;
            Upsert(key, value);
            return true;
        }
    }

    public bool TryAtomicAddOrUpdate(in TKey key, in TValue valueToAdd, Func<TValue, TValue> valueToUpdateGetter)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        AddOrUpdateResult status;
        IMutableSegment<TKey, TValue> segmentZero;
        while (true)
        {
            lock (AtomicUpdateLock)
            {
                segmentZero = SegmentZero;
                if (segmentZero.IsFrozen)
                {
                    status = AddOrUpdateResult.RETRY_SEGMENT_IS_FULL;
                }
                else if (segmentZero.TryGet(in key, out var existing))
                {
                    var value = valueToUpdateGetter(existing);
                    status = segmentZero.Upsert(key, value);
                }
                else if (TryGetFromReadonlySegments(in key, out existing))
                {
                    var value = valueToUpdateGetter(existing);
                    status = segmentZero.Upsert(key, value);
                }
                else
                {
                    status = segmentZero.Upsert(key, valueToAdd);
                }
            }
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    continue;
                default:
                    return status == AddOrUpdateResult.ADDED;
            }
        }
    }

    public void AtomicUpsert(in TKey key, in TValue value)
    {
        lock (AtomicUpdateLock)
        {
            Upsert(in key, in value);
        }
    }

    public void Upsert(in TKey key, in TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        while(true)
        {
            var segmentZero = SegmentZero;
            var status = segmentZero.Upsert(key, value);
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    continue;
                default:
                    return;
            }
        }
    }

    public bool TryDelete(in TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        if (!ContainsKey(key))
            return false;
        ForceDelete(in key);
        return true;
    }

    public void ForceDelete(in TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));
        while(true)
        {
            var segmentZero = SegmentZero;
            var status = segmentZero.Delete(key);
            switch (status)
            {
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FROZEN:
                    ForceDelete(key);
                    continue;
                case AddOrUpdateResult.RETRY_SEGMENT_IS_FULL:
                    MoveSegmentZeroForward(segmentZero);
                    ForceDelete(key);
                    continue;
                default: return;
            }

        }
    }

    /// <summary>
    /// Moves mutable segment into readonly segment.
    /// This will clear the writable region of the LSM tree.
    /// This method is thread safe and can be called from many threads.
    /// The movement only occurs 
    /// if the current segment zero is the given segment zero.
    /// </summary>
    /// <param name="segmentZero">The segment zero to move forward.</param>
    private void MoveSegmentZeroForward(IMutableSegment<TKey, TValue> segmentZero)
    {
        lock (AtomicUpdateLock)
        {
            // move segment zero only if
            // the given segment zero is the current segment zero (not already moved)
            // and it is not frozen.
            if (segmentZero.IsFrozen || segmentZero != SegmentZero)
                return;

            //Don't move empty segment zero.
            int c = segmentZero.Length;
            if (c == 0)
                return;

            segmentZero.Freeze();
            ReadOnlySegmentQueue.Enqueue(segmentZero);
            MetaWal.EnqueueReadOnlySegment(segmentZero.SegmentId);
            SegmentZero = new MutableSegment<TKey, TValue>(Options, IncrementalIdProvider.NextId());
            MetaWal.NewSegmentZero(SegmentZero.SegmentId);
        }
        OnSegmentZeroMovedForward?.Invoke(this);
    }

    public void MoveSegmentZeroForward()
    {
        MoveSegmentZeroForward(SegmentZero);
    }

    public void SaveMetaData()
    {
        lock (ShortMergerLock)
        lock (AtomicUpdateLock)
        {
            MetaWal.SaveMetaData(
                ZoneTreeMeta,
                SegmentZero.SegmentId,
                DiskSegment.SegmentId,
                ReadOnlySegmentQueue.Select(x => x.SegmentId).Reverse().ToArray());
        }
    }

    public async ValueTask<MergeResult> StartMergeOperation()
    {
        OnMergeOperationStarted?.Invoke(this);
        var mergeResult = await StartMergeOperationInternal();
        OnMergeOperationEnded?.Invoke(this, mergeResult);
        return mergeResult;
    }

    private async ValueTask<MergeResult> StartMergeOperationInternal()
    {
        if (IsMerging)
            return MergeResult.ANOTHER_MERGE_IS_RUNNING;
        IsCancelMergeRequested = false;
        return await Task.Run(() =>
        {
            lock (LongMergerLock)
            {
                try
                {
                    if (IsMerging)
                        return MergeResult.ANOTHER_MERGE_IS_RUNNING;
                    IsMerging = true;
                    return MergeReadOnlySegmentsInternal();
                }
                finally
                {
                    IsMerging = false;
                }
            }
        });
    }

    public void TryCancelMergeOperation()
    {
        IsCancelMergeRequested = true;
    }

    private MergeResult MergeReadOnlySegmentsInternal()
    {
        var oldDiskSegment = DiskSegment;
        var roSegments = ReadOnlySegmentQueue.ToArray();

        if (roSegments.Any(x => !x.IsFullyFrozen))
            return MergeResult.RETRY_READONLY_SEGMENTS_ARE_NOT_READY;

        var readOnlySegmentsArray = roSegments.Select(x => x.GetSeekableIterator()).ToArray();
        if (readOnlySegmentsArray.Length == 0)
            return MergeResult.NOTHING_TO_MERGE;

        var mergingSegments = new List<ISeekableIterator<TKey, TValue>>();
        mergingSegments.AddRange(readOnlySegmentsArray.Reverse());
        mergingSegments.Add(oldDiskSegment.GetSeekableIterator());

        if (IsCancelMergeRequested)
            return MergeResult.CANCELLED_BY_USER;

        var len = mergingSegments.Count;
        var diskSegmentCreator = new DiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider);
        var heap = new FixedSizeMinHeap<HeapEntry<TKey, TValue>>(len + 1, MinHeapEntryComparer);

        var fillHeap = () =>
        {
            for (int i = 0; i < len; i++)
            {
                var s = mergingSegments[i];
                if (!s.Next())
                    continue;
                var key = s.CurrentKey;
                var value = s.CurrentValue;
                var entry = new HeapEntry<TKey, TValue>(key, value, i);
                heap.Insert(entry);
            }
        };

        int minSegmentIndex = 0;

        var skipElement = () =>
        {
            var minSegment = mergingSegments[minSegmentIndex];
            if (minSegment.Next())
            {
                var key = minSegment.CurrentKey;
                var value = minSegment.CurrentValue;
                heap.ReplaceMin(new HeapEntry<TKey, TValue>(key, value, minSegmentIndex));
            }
            else
            {
                heap.RemoveMin();
            }
        };
        fillHeap();
        var comparer = Options.Comparer;
        var hasPrev = false;
        TKey prevKey = default;
        while (heap.Count > 0)
        {
            if (IsCancelMergeRequested)
            {
                try
                {
                    diskSegmentCreator.DropDiskSegment();
                }
                catch (Exception e)
                {
                    OnCanNotDropDiskSegmentCreator?.Invoke(diskSegmentCreator, e);
                }
                return MergeResult.CANCELLED_BY_USER;
            }

            var minEntry = heap.MinValue;
            minSegmentIndex = minEntry.SegmentIndex;

            // ignore deleted entries.
            if (IsValueDeleted(minEntry.Value))
            {
                skipElement();
                prevKey = minEntry.Key;
                hasPrev = true;
                continue;
            }

            if (hasPrev && comparer.Compare(minEntry.Key, prevKey) == 0)
            {
                skipElement();
                continue;
            }

            prevKey = minEntry.Key;
            hasPrev = true;

            diskSegmentCreator.Append(minEntry.Key, minEntry.Value);
            skipElement();
        }

        var newDiskSegment = diskSegmentCreator.CreateReadOnlyDiskSegment();
        newDiskSegment.DropFailureReporter = (ds, e) => ReportDropFailure(ds, e);
        OnDiskSegmentCreated?.Invoke(this, newDiskSegment);
        lock (ShortMergerLock)
        {
            DiskSegment = newDiskSegment;
            MetaWal.NewDiskSegment(newDiskSegment.SegmentId);
            try
            {
                oldDiskSegment.Drop();
            }
            catch (Exception e)
            {
                OnCanNotDropDiskSegment?.Invoke(oldDiskSegment, e);
            }

            len = readOnlySegmentsArray.Length;
            while (len > 0)
            {
                ReadOnlySegmentQueue.TryDequeue(out var segment);
                MetaWal.DequeueReadOnlySegment(segment.SegmentId);
                try
                {
                    segment.Drop();
                }
                catch (Exception e)
                {
                    OnCanNotDropReadOnlySegment.Invoke(segment, e);
                }
                --len;
            }
        }
        OnDiskSegmentActivated?.Invoke(this, newDiskSegment);
        return MergeResult.SUCCESS;
    }

    private void ReportDropFailure(IDiskSegment<TKey, TValue> ds, Exception e)
    {
        OnCanNotDropDiskSegment?.Invoke(ds, e);
    }

    public void Dispose()
    {
        OnZoneTreeIsDisposing?.Invoke(this);
        SegmentZero.ReleaseResources();
        DiskSegment.Dispose();
        MetaWal.Dispose();
        foreach (var ros in ReadOnlySegments)
            ros.ReleaseResources();
    }

    public void DestroyTree()
    {
        MetaWal.Dispose();
        SegmentZero.Drop();
        DiskSegment.Drop();
        DiskSegment.Dispose();
        var readOnlySegments = ReadOnlySegmentQueue.ToArray();
        foreach (var ros in readOnlySegments)
            ros.Drop();
        Options.WriteAheadLogProvider.DropStore();
        Options.RandomAccessDeviceManager.DropStore();
    }

    public int Count()
    {
        var iterator = CreateInMemorySegmentsIterator(
            autoRefresh: false,
            includeDeletedRecords: true);

        IDiskSegment<TKey, TValue> diskSegment = null;
        lock(ShortMergerLock)
        lock (AtomicUpdateLock)
        {
            // 2 things to synchronize with
            // MoveSegmentForward and disk merger segment swap.
            diskSegment = DiskSegment;
            iterator.Refresh();
        }        
        var count = diskSegment.Length;
        
        while(iterator.Next())
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
                if(!isValueDeleted)
                    ++count;
            }
        }
        return count;
    }

    public SegmentCollection CollectSegments(
        bool includeSegmentZero,
        bool includeDiskSegment)
    {
        lock (ShortMergerLock)
        lock (AtomicUpdateLock)
        {
            var roSegments = ReadOnlySegmentQueue.ToArray();
            var seekableIterators = new List<ISeekableIterator<TKey, TValue>>();
            if (includeSegmentZero)
                seekableIterators.Add(SegmentZero.GetSeekableIterator());

            var readOnlySegmentsArray = roSegments.Select(x => x.GetSeekableIterator()).ToArray();
            seekableIterators.AddRange(readOnlySegmentsArray.Reverse());

            var result = new SegmentCollection
            {
                SeekableIterators = seekableIterators
            };

            if (includeDiskSegment)
            {
                var diskSegment = DiskSegment;
                diskSegment.AddReader();
                result.DiskSegment = diskSegment;
                seekableIterators.Add(diskSegment.GetSeekableIterator());
            }
            return result;
        }
    }

    public class SegmentCollection
    {
        public IReadOnlyList<ISeekableIterator<TKey, TValue>> SeekableIterators { get; set; }

        public IDiskSegment<TKey, TValue> DiskSegment { get; set; }
    }

    public IZoneTreeIterator<TKey, TValue> CreateIterator(bool autoRefresh, bool includeDeletedRecords)
    {
        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MinHeapEntryComparer,
            autoRefresh: autoRefresh,
            isReverseIterator: false,
            includeDeletedRecords,
            includeSegmentZero: true,
            includeDiskSegment: true);
        return iterator;
    }

    public IZoneTreeIterator<TKey, TValue> CreateReverseIterator(bool autoRefresh, bool includeDeletedRecords)
    {
        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MaxHeapEntryComparer,
            autoRefresh: autoRefresh,
            isReverseIterator: true,
            includeDeletedRecords,
            includeSegmentZero: true,
            includeDiskSegment: true);
        return iterator;
    }

    /// <summary>
    /// Creates an iterator that enables scanning of the readonly segments.
    /// </summary>
    /// <returns>ZoneTree Iterator</returns>
    public IZoneTreeIterator<TKey, TValue> CreateReadOnlySegmentsIterator(bool autoRefresh, bool includeDeletedRecords)
    {
        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MinHeapEntryComparer,
            autoRefresh: autoRefresh,
            isReverseIterator: false,
            includeDeletedRecords,
            includeSegmentZero: false,
            includeDiskSegment: false);
        return iterator;
    }

    /// <summary>
    /// Creates an iterator that enables scanning of the in memory segments.
    /// This includes readonly segments and segment zero (mutable segment).
    /// </summary>
    /// <param name="includeDeletedRecords">if true the deleted records are included in iteration.</param>
    /// <returns>ZoneTree Iterator</returns>
    public IZoneTreeIterator<TKey, TValue> 
        CreateInMemorySegmentsIterator(bool autoRefresh, bool includeDeletedRecords)
    {
        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MinHeapEntryComparer,
            autoRefresh: autoRefresh,
            isReverseIterator: false,
            includeDeletedRecords,
            includeSegmentZero: true,
            includeDiskSegment: false);
        return iterator;
    }
}
