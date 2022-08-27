﻿using System.Diagnostics;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    /// <summary>
    /// Moves mutable segment into readonly segment.
    /// This will clear the writable region of the LSM tree.
    /// This method is thread safe and can be called from many threads.
    /// The movement only occurs 
    /// if the current segment zero is the given segment zero.
    /// </summary>
    /// <param name="segmentZero">The segment zero to move forward.</param>
    void MoveSegmentZeroForward(IMutableSegment<TKey, TValue> segmentZero)
    {
        lock (AtomicUpdateLock)
        {
            // move segment zero only if
            // the given segment zero is the current segment zero (not already moved)
            // and it is not frozen.
            if (segmentZero.IsFrozen || segmentZero != SegmentZero)
                return;

            //Don't move empty segment zero.
            var c = segmentZero.Length;
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

    void StartMergeOperationInternal()
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
                Logger.LogError(e);
                OnMergeOperationEnded?.Invoke(this, MergeResult.FAILURE);
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

    readonly int ReadOnlySegmentFullyFrozenSpinTimeout = 1000;

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

        var enableMultiPartDiskSegment =
            Options.DiskSegmentOptions.DiskSegmentMode == DiskSegmentMode.MultiPartDiskSegment;

        var len = mergingSegments.Count;
        var diskSegmentIndex = len - 1;

        using IDiskSegmentCreator<TKey, TValue> diskSegmentCreator = 
            enableMultiPartDiskSegment ? 
            new MultiPartDiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider) :
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

        var firstKeysOfEveryPart = oldDiskSegment.GetFirstKeysOfEveryPart();
        var lastKeysOfEveryPart = oldDiskSegment.GetLastKeysOfEveryPart();
        var lastValuesOfEveryPart = oldDiskSegment.GetLastValuesOfEveryPart();
        var diskSegmentMinimumRecordCount = Options.DiskSegmentOptions.MinimumRecordCount;

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
                    Logger.LogError(e);
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
            var currentPartIndex = -1;
            if (isDiskSegmentKey)
            {
                var diskIterator = mergingSegments[minSegmentIndex];
                iteratorPosition =
                    diskIterator.IsBeginningOfAPart ?
                    IteratorPosition.BeginningOfAPart :
                    diskIterator.IsEndOfAPart ?
                    IteratorPosition.EndOfAPart :
                    IteratorPosition.MiddleOfAPart;
                currentPartIndex = diskIterator.GetPartIndex();
            }

            // skip a part without merge if possible
            if (enableMultiPartDiskSegment &&
                isDiskSegmentKey &&
                iteratorPosition == IteratorPosition.BeginningOfAPart)
            {
                var part = oldDiskSegment
                    .GetPart(currentPartIndex);
                if (part.Length > diskSegmentMinimumRecordCount &&
                    diskSegmentCreator.CanSkipCurrentPart)
                {
                    var lastKey = lastKeysOfEveryPart[currentPartIndex];
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
                            part, 
                            minEntry.Key, 
                            lastKey,
                            minEntry.Value, 
                            lastValuesOfEveryPart[currentPartIndex]);
                        mergingSegments[diskSegmentIndex].Skip(part.Length - 2);
                        prevKey = lastKey;
                        skipElement();
                        ++skipCount;
                        continue;
                    }
                }
                ++dropCount;
                Logger.LogTrace(
                    $"drop: {part.SegmentId} ({dropCount} / {skipCount + dropCount})");

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
                oldDiskSegment.Drop(diskSegmentCreator.AppendedPartSegmentIds);
            }
            catch (Exception e)
            {
                Logger.LogError(e);
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
                    Logger.LogError(e);
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
}