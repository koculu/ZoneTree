using System.Diagnostics;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public Thread StartBottomSegmentsMergeOperation(int from, int to)
    {
        if (from >= to)
        {
            throw new InvalidMergeRangeException(from, to);
        }
        if (IsBottomSegmentsMerging)
        {
            OnBottomSegmentsMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
            return null;
        }

        OnBottomSegmentsMergeOperationStarted?.Invoke(this);
        var thread = new Thread(() => StartBottomSegmentsMergeOperationInternal(from, to));
        thread.Start();
        return thread;
    }

    void StartBottomSegmentsMergeOperationInternal(int from, int to)
    {
        if (IsBottomSegmentsMerging)
        {
            OnBottomSegmentsMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
            return;
        }
        IsCancelBottomSegmentsMergeRequested = false;
        lock (LongBottomSegmentsMergerLock)
        {
            try
            {
                if (IsBottomSegmentsMerging)
                {
                    OnBottomSegmentsMergeOperationEnded?.Invoke(this, MergeResult.ANOTHER_MERGE_IS_RUNNING);
                    return;
                }
                IsBottomSegmentsMerging = true;
                var mergeResult = MergeBottomSegmentsInternal(from, to);
                IsBottomSegmentsMerging = false;
                OnBottomSegmentsMergeOperationEnded?.Invoke(this, mergeResult);

            }
            catch (Exception e)
            {
                Logger.LogError(e);
                OnBottomSegmentsMergeOperationEnded?.Invoke(this, MergeResult.FAILURE);
            }
            finally
            {
                IsBottomSegmentsMerging = false;
            }
        }
    }

    public void TryCancelBottomSegmentsMergeOperation()
    {
        IsCancelBottomSegmentsMergeRequested = true;
    }

    MergeResult MergeBottomSegmentsInternal(int from, int to)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var bottomSegments = BottomSegmentQueue.ToLastInFirstArray();

        var writeDeletedValues = from > 0;

        var mergingSegments = bottomSegments
            .Select(x => x.GetSeekableIterator())
            .Skip(from)
            .Take(to - from + 1)
            .ToArray();
        to = from + mergingSegments.Length - 1;
        if (mergingSegments.Length == 0)
            return MergeResult.NOTHING_TO_MERGE;
        var bottomDiskSegment = bottomSegments[from];
        Logger.LogTrace($"Bottom Segments Merge started." +
            $" from: {from} - to: {to} out of: {bottomSegments.Count} ");

        if (mergingSegments.Length == 0)
            return MergeResult.NOTHING_TO_MERGE;

        if (IsCancelBottomSegmentsMergeRequested)
        {
            // Do not remove null assignments because of GC issue!
            bottomSegments = null;
            mergingSegments = null;
            bottomDiskSegment = null;
            return MergeResult.CANCELLED_BY_USER;
        }

        var enableMultiPartDiskSegment =
            Options.DiskSegmentOptions.DiskSegmentMode == DiskSegmentMode.MultiPartDiskSegment;

        var len = mergingSegments.Length;
        var bottomIndex = len - 1;

        using IDiskSegmentCreator<TKey, TValue> diskSegmentCreator =
            enableMultiPartDiskSegment ?
            new MultiPartDiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider) :
            new DiskSegmentCreator<TKey, TValue>(Options, IncrementalIdProvider);

        var heap = new FixedSizeMinHeap<HeapEntry<TKey, TValue>>(len + 1, MinHeapEntryComparer);

        void fillHeap()
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
        }

        int minSegmentIndex = 0;

        void skipElement()
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
        }
        fillHeap();
        var comparer = Options.Comparer;
        var hasPrev = false;
        TKey prevKey = default;

        var firstKeysOfEveryPart = bottomDiskSegment.GetFirstKeysOfEveryPart();
        var lastKeysOfEveryPart = bottomDiskSegment.GetLastKeysOfEveryPart();
        var lastValuesOfEveryPart = bottomDiskSegment.GetLastValuesOfEveryPart();
        var diskSegmentMinimumRecordCount = Options.DiskSegmentOptions.MinimumRecordCount;

        var dropCount = 0;
        var skipCount = 0;
        while (heap.Count > 0)
        {
            if (IsCancelBottomSegmentsMergeRequested)
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
                // Do not remove null assignments because of GC issue!
                bottomSegments = null;
                mergingSegments = null;
                bottomDiskSegment = null;
                heap = null;
                return MergeResult.CANCELLED_BY_USER;
            }

            var minEntry = heap.MinValue;
            minSegmentIndex = minEntry.SegmentIndex;

            // ignore deleted entries if writeDeletedValues is false.
            if (!writeDeletedValues && IsValueDeleted(minEntry.Value))
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
            var isDiskSegmentKey = minSegmentIndex == bottomIndex;
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
                var part = bottomDiskSegment
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
                        mergingSegments[bottomIndex].Skip(part.Length - 2);
                        prevKey = lastKey;
                        skipElement();
                        ++skipCount;
                        continue;
                    }
                }
                ++dropCount;
                Logger.LogTrace(new LogMergerDrop(part.SegmentId, dropCount, skipCount));

            }

            diskSegmentCreator.Append(minEntry.Key, minEntry.Value, iteratorPosition);
            skipElement();
        }

        var newDiskSegment = diskSegmentCreator.CreateReadOnlyDiskSegment();
        newDiskSegment.DropFailureReporter = (ds, e) => ReportDropFailure(ds, e);
        OnDiskSegmentCreated?.Invoke(this, newDiskSegment, true);
        lock (ShortMergerLock)
        {
            bottomSegments = BottomSegmentQueue.ToLastInFirstArray();
            var bottomSegmentsLength = bottomSegments.Count;
            var queue = new Queue<IDiskSegment<TKey, TValue>>();
            for (var i = 0; i < bottomSegmentsLength; ++i)
            {
                var ri = bottomSegmentsLength - i - 1;
                if (i > from && i <= to)
                {
                    MetaWal.DeleteBottomSegment(bottomSegments[i].SegmentId);
                    continue;
                }
                if (i == from)
                {
                    MetaWal.InsertBottomSegment(newDiskSegment.SegmentId, i);
                    MetaWal.DeleteBottomSegment(bottomSegments[i].SegmentId);
                    queue.Enqueue(newDiskSegment);
                }
                else
                {
                    queue.Enqueue(bottomSegments[i]);
                }
            }
            var newQueue = new SingleProducerSingleConsumerQueue<IDiskSegment<TKey, TValue>>(queue.Reverse());
            BottomSegmentQueue = newQueue;

            for (var i = from; i <= to; ++i)
            {
                var ri = bottomSegmentsLength - i - 1;
                var diskSegmentToDrop = bottomSegments[i];
                try
                {
                    diskSegmentToDrop.Drop(diskSegmentCreator.AppendedPartSegmentIds);
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                    OnCanNotDropDiskSegment?.Invoke(diskSegmentToDrop, e);
                }
            }
        }

        TotalBottomSegmentsMergeSkipCount += skipCount;
        TotalBottomSegmentsMergeDropCount += dropCount;
        Logger.LogTrace(
            new LogMergerSuccess(
                dropCount,
                skipCount,
                stopwatch.ElapsedMilliseconds,
                TotalBottomSegmentsMergeDropCount,
                TotalBottomSegmentsMergeSkipCount));

        // Do not remove null assignments below and anywhere in this function!
        // GC does not collect local variables,
        // when this method is called by another thread.
        bottomSegments = null;
        mergingSegments = null;
        bottomDiskSegment = null;
        heap = null;
        OnDiskSegmentActivated?.Invoke(this, newDiskSegment, true);
        return MergeResult.SUCCESS;
    }

    int TotalBottomSegmentsMergeSkipCount;
    int TotalBottomSegmentsMergeDropCount;
}
