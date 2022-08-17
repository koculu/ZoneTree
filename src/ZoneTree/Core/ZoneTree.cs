﻿using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Extensions;
using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public const string SegmentWalCategory = "seg";

    public ILogger Logger { get; }

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

    public int MutableSegmentRecordCount => SegmentZero.Length;

    public int InMemoryRecordCount
    {
        get
        {
            lock (AtomicUpdateLock)
            {
                return SegmentZero.Length + ReadOnlySegmentsRecordCount;
            }
        }
    }

    public int TotalRecordCount
    {
        get
        {
            lock (ShortMergerLock)
            {
                return InMemoryRecordCount + DiskSegment.Length;
            }
        }
    }

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
        SegmentZero = new MutableSegment<TKey, TValue>(
            options, IncrementalIdProvider.NextId(), new IncrementalIdProvider());
        IsValueDeleted = options.IsValueDeleted;
        FillZoneTreeMeta();
        MetaWal.SaveMetaData(
            ZoneTreeMeta,
            SegmentZero.SegmentId,
            DiskSegment.SegmentId,
            Array.Empty<long>(),
            true);
    }

    public ZoneTree(
        ZoneTreeOptions<TKey, TValue> options,
        ZoneTreeMeta meta,
        IReadOnlyList<IReadOnlySegment<TKey, TValue>> readOnlySegments,
        IMutableSegment<TKey, TValue> segmentZero,
        IDiskSegment<TKey, TValue> diskSegment,
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

        ZoneTreeMeta.WriteAheadLogMode = Options.WriteAheadLogProvider.WriteAheadLogMode;
        ZoneTreeMeta.EnableDiskSegmentCompression = Options.EnableDiskSegmentCompression;
        ZoneTreeMeta.DiskSegmentCompressionBlockSize = Options.DiskSegmentCompressionBlockSize;
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
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

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
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();

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
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
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
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (AtomicUpdateLock)
        {
            Upsert(in key, in value);
        }
    }

    public void Upsert(in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        while (true)
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
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        if (!ContainsKey(key))
            return false;
        ForceDelete(in key);
        return true;
    }

    public void ForceDelete(in TKey key)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        while (true)
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

            SegmentZero = new MutableSegment<TKey, TValue>(
                Options, IncrementalIdProvider.NextId(),
                segmentZero.OpIndexProvider);
            MetaWal.NewSegmentZero(SegmentZero.SegmentId);
        }
        OnSegmentZeroMovedForward?.Invoke(this);
    }

    public void MoveSegmentZeroForward()
    {
        lock (AtomicUpdateLock)
        {
            MoveSegmentZeroForward(SegmentZero);
        }
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

    public Thread StartMergeOperation()
    {
        if (IsMerging)
        {
            OnMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
            return null;
        }
            
        OnMergeOperationStarted?.Invoke(this);
        var thread = new Thread(StartMergeOperationInternal);
        thread.Start();
        return thread;
    }

    private void StartMergeOperationInternal()
    {
        if (IsMerging)
        {
            OnMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
            return;
        }
        IsCancelMergeRequested = false;
        lock (LongMergerLock)
        {
            try
            {
                if (IsMerging)
                {
                    OnMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
                    return;
                }
                IsMerging = true;
                var mergeResult = MergeReadOnlySegmentsInternal();
                IsMerging = false;
                OnMergeOperationEnded?.Invoke(this, mergeResult);

            }
            catch (Exception e)
            {
                Logger.LogException(e);
                OnMergeOperationEnded?.Invoke(this, MergeResult.FAILURE);
                throw;
            }
            finally
            {
                IsMerging = false;
            }
        }
    }

    public void TryCancelMergeOperation()
    {
        IsCancelMergeRequested = true;
    }

    int ReadOnlySegmentFullyFrozenSpinTimeout = 1000;

    MergeResult MergeReadOnlySegmentsInternal()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        Logger.LogTrace("Merge started.");

        var oldDiskSegment = DiskSegment;
        var roSegments = ReadOnlySegmentQueue.ToArray();

        if (roSegments.Any(x => !x.IsFullyFrozen))
        {
            SpinWait.SpinUntil(() => !roSegments.Any(x => !x.IsFullyFrozen), 
                ReadOnlySegmentFullyFrozenSpinTimeout);
            if (roSegments.Any(x => !x.IsFullyFrozen))
                return MergeResult.RETRY_READONLY_SEGMENTS_ARE_NOT_READY;
        }

        var readOnlySegmentsArray = roSegments.Select(x => x.GetSeekableIterator()).ToArray();
        if (readOnlySegmentsArray.Length == 0)
            return MergeResult.NOTHING_TO_MERGE;

        var mergingSegments = new List<ISeekableIterator<TKey, TValue>>();
        mergingSegments.AddRange(readOnlySegmentsArray.Reverse());
        if (oldDiskSegment is not NullDiskSegment<TKey, TValue>)
            mergingSegments.Add(oldDiskSegment.GetSeekableIterator());

        if (IsCancelMergeRequested)
            return MergeResult.CANCELLED_BY_USER;

        var enableMultiSectorDiskSegment =
            Options.DiskSegmentMode == DiskSegmentMode.MultipleDiskSegments;

        var len = mergingSegments.Count;
        var diskSegmentIndex = len - 1;

        using IDiskSegmentCreator<TKey, TValue> diskSegmentCreator = 
            enableMultiSectorDiskSegment ? 
            new MultiSectorDiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider) :
            new DiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider);

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

        var firstKeysOfEverySector = oldDiskSegment.GetFirstKeysOfEverySector();
        var lastKeysOfEverySector = oldDiskSegment.GetLastKeysOfEverySector();
        var lastValuesOfEverySector = oldDiskSegment.GetLastValuesOfEverySector();
        var diskSegmentMinimumRecordCount = Options.DiskSegmentMinimumRecordCount;

        var dropCount = 0;
        var skipCount = 0;
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
                    Logger.LogException(e);
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
            var isDiskSegmentKey = minSegmentIndex == diskSegmentIndex;
            var iteratorPosition = IteratorPosition.None;
            var currentSectorIndex = -1;
            if (isDiskSegmentKey)
            {
                var diskIterator = mergingSegments[minSegmentIndex];
                iteratorPosition =
                    diskIterator.IsBeginningOfASector ?
                    IteratorPosition.BeginningOfASector :
                    diskIterator.IsEndOfASector ?
                    IteratorPosition.EndOfASector :
                    IteratorPosition.MiddleOfASector;
                currentSectorIndex = diskIterator.GetSectorIndex();
            }

            // skip a sector without merge if possible
            if (enableMultiSectorDiskSegment &&
                isDiskSegmentKey &&
                iteratorPosition == IteratorPosition.BeginningOfASector)
            {
                var sector = oldDiskSegment
                    .GetSector(currentSectorIndex);
                if (sector.Length > diskSegmentMinimumRecordCount &&
                    diskSegmentCreator.CanSkipCurrentSector)
                {
                    var lastKey = lastKeysOfEverySector[currentSectorIndex];
                    var islastKeySmallerThanAllOtherKeys = true;
                    var heapKeys = heap.GetKeys();
                    var heapKeysLen = heapKeys.Length;
                    for (int i = 0; i < heapKeysLen; i++)
                    {
                        var s = heapKeys[i];
                        if (s.SegmentIndex == minSegmentIndex)
                            continue;
                        var key = s.Key;
                        if (comparer.Compare(lastKey, key) >= 0)
                        {
                            islastKeySmallerThanAllOtherKeys = false;
                            break;
                        }
                    }
                    if (islastKeySmallerThanAllOtherKeys)
                    {
                        diskSegmentCreator.Append(
                            sector, 
                            minEntry.Key, 
                            lastKey,
                            minEntry.Value, 
                            lastValuesOfEverySector[currentSectorIndex]);
                        mergingSegments[diskSegmentIndex].Skip(sector.Length - 2);
                        prevKey = lastKey;
                        skipElement();
                        ++skipCount;
                        continue;
                    }
                }
                ++dropCount;
                Logger.LogTrace(
                    $"drop: {sector.SegmentId} ({dropCount} / {skipCount + dropCount})");

            }
            
            diskSegmentCreator.Append(minEntry.Key, minEntry.Value, iteratorPosition);
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
                oldDiskSegment.Drop(diskSegmentCreator.AppendedSectorSegmentIds);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
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
                    Logger.LogException(e);
                    OnCanNotDropReadOnlySegment?.Invoke(segment, e);
                }
                --len;
            }
        }

        TotalSkipCount += skipCount;
        TotalDropCount += dropCount;
        Logger.LogTrace($"Merge SUCCESS in {stopwatch.ElapsedMilliseconds} ms ({dropCount} / {skipCount + dropCount})");
        var total = TotalSkipCount + TotalDropCount;
        var dropPercentage = 1.0 * TotalDropCount / (total == 0 ? 1 : total);
        Logger.LogTrace($"Total Drop Ratio ({TotalDropCount} / {TotalSkipCount + TotalDropCount}) => {dropPercentage*100:0.##}%");

        OnDiskSegmentActivated?.Invoke(this, newDiskSegment);
        return MergeResult.SUCCESS;
    }

    int TotalSkipCount;
    int TotalDropCount;

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
        lock (ShortMergerLock)
            lock (AtomicUpdateLock)
            {
                // 2 things to synchronize with
                // MoveSegmentForward and disk merger segment swap.
                diskSegment = DiskSegment;
                iterator.Refresh();
            }
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
                    if (diskSegment is not NullDiskSegment<TKey, TValue>)
                    {
                        diskSegment.AddReader();
                        result.DiskSegment = diskSegment;
                        seekableIterators.Add(diskSegment.GetSeekableIterator());
                    }
                }
                return result;
            }
    }

    public class SegmentCollection
    {
        public IReadOnlyList<ISeekableIterator<TKey, TValue>> SeekableIterators { get; set; }

        public IDiskSegment<TKey, TValue> DiskSegment { get; set; }
    }

    public IZoneTreeIterator<TKey, TValue> CreateIterator(
        IteratorType iteratorType, bool includeDeletedRecords)
    {
        var includeSegmentZero = iteratorType is not IteratorType.Snapshot and
            not IteratorType.ReadOnlyRegion;

        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MinHeapEntryComparer,
            autoRefresh: iteratorType == IteratorType.AutoRefresh,
            isReverseIterator: false,
            includeDeletedRecords,
            includeSegmentZero: includeSegmentZero,
            includeDiskSegment: true);

        if (iteratorType == IteratorType.Snapshot)
        {
            MoveSegmentZeroForward();
            iterator.Refresh();
            iterator.WaitUntilReadOnlySegmentsBecomeFullyFrozen();
        }
        else if (iteratorType == IteratorType.ReadOnlyRegion)
        {
            iterator.Refresh();
            iterator.WaitUntilReadOnlySegmentsBecomeFullyFrozen();
        }
        return iterator;
    }

    public IZoneTreeIterator<TKey, TValue> CreateReverseIterator(
        IteratorType iteratorType, bool includeDeletedRecords)
    {
        var includeSegmentZero = iteratorType is not IteratorType.Snapshot and
            not IteratorType.ReadOnlyRegion;

        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MaxHeapEntryComparer,
            autoRefresh: iteratorType == IteratorType.AutoRefresh,
            isReverseIterator: true,
            includeDeletedRecords,
            includeSegmentZero: includeSegmentZero,
            includeDiskSegment: true);

        if (iteratorType == IteratorType.Snapshot)
        {
            MoveSegmentZeroForward();
            iterator.Refresh();
            iterator.WaitUntilReadOnlySegmentsBecomeFullyFrozen();
        }
        else if (iteratorType == IteratorType.ReadOnlyRegion)
        {
            iterator.Refresh();
            iterator.WaitUntilReadOnlySegmentsBecomeFullyFrozen();
        }

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
