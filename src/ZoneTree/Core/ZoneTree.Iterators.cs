using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public SegmentCollection CollectSegments(
        bool includeSegmentZero,
        bool includeDiskSegment,
        bool includeBottomSegments)
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

                if (includeBottomSegments)
                {
                    var bottomSegments = BottomSegmentQueue.Reverse().ToArray();
                    foreach (var bottom in bottomSegments)
                    {
                        bottom.AddReader();
                        result.BottomSegments = bottomSegments;
                        seekableIterators.Add(bottom.GetSeekableIterator());

                    }
                }
                return result;
            }
    }

    public class SegmentCollection
    {
        public IReadOnlyList<ISeekableIterator<TKey, TValue>> SeekableIterators { get; set; }

        public IDiskSegment<TKey, TValue> DiskSegment { get; set; }
        
        public IDiskSegment<TKey, TValue>[] BottomSegments { get; set; }
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
            includeDiskSegment: true,
            includeBottomSegments: true);

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
            includeDiskSegment: true,
            includeBottomSegments: true);

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
            includeDiskSegment: false,
            includeBottomSegments: false);
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
            includeDiskSegment: false,
            includeBottomSegments: false);
        return iterator;
    }
}
