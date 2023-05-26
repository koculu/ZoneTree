using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public sealed partial class ZoneTree<TKey, TValue> : IZoneTree<TKey, TValue>, IZoneTreeMaintenance<TKey, TValue>
{
    public SegmentCollection CollectSegments(
        bool includeMutableSegment,
        bool includeDiskSegment,
        bool includeBottomSegments)
    {
        lock (ShortMergerLock)
            lock (AtomicUpdateLock)
            {
                var roSegments = ReadOnlySegmentQueue.ToLastInFirstArray();
                var seekableIterators = new List<ISeekableIterator<TKey, TValue>>();
                if (includeMutableSegment)
                    seekableIterators.Add(MutableSegment.GetSeekableIterator());

                var readOnlySegmentsArray = roSegments.Select(x => x.GetSeekableIterator()).ToArray();
                seekableIterators.AddRange(readOnlySegmentsArray);

                var result = new SegmentCollection
                {
                    SeekableIterators = seekableIterators
                };

                if (includeDiskSegment)
                {
                    var diskSegment = DiskSegment;
                    if (diskSegment is not NullDiskSegment<TKey, TValue>)
                    {
                        diskSegment.AttachIterator();
                        result.DiskSegment = diskSegment;
                        seekableIterators.Add(diskSegment.GetSeekableIterator());
                    }
                }

                if (includeBottomSegments)
                {
                    var bottomSegments = BottomSegmentQueue.ToLastInFirstArray();
                    foreach (var bottom in bottomSegments)
                    {
                        bottom.AttachIterator();
                        seekableIterators.Add(bottom.GetSeekableIterator());
                    }
                    result.BottomSegments = bottomSegments;
                }
                return result;
            }
    }

    public sealed class SegmentCollection
    {
        public IReadOnlyList<ISeekableIterator<TKey, TValue>> SeekableIterators { get; set; }

        public IDiskSegment<TKey, TValue> DiskSegment { get; set; }

        public IReadOnlyList<IDiskSegment<TKey, TValue>> BottomSegments { get; set; }
    }

    public IZoneTreeIterator<TKey, TValue> CreateIterator(
        IteratorType iteratorType, bool includeDeletedRecords)
    {
        var includeMutableSegment = iteratorType is not IteratorType.Snapshot and
            not IteratorType.ReadOnlyRegion;

        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MinHeapEntryComparer,
            autoRefresh: iteratorType == IteratorType.AutoRefresh,
            isReverseIterator: false,
            includeDeletedRecords,
            includeMutableSegment: includeMutableSegment,
            includeDiskSegment: true,
            includeBottomSegments: true);

        if (iteratorType == IteratorType.Snapshot)
        {
            MoveMutableSegmentForward();
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
        var includeMutableSegment = iteratorType is not IteratorType.Snapshot and
            not IteratorType.ReadOnlyRegion;

        var iterator = new ZoneTreeIterator<TKey, TValue>(
            Options,
            this,
            MaxHeapEntryComparer,
            autoRefresh: iteratorType == IteratorType.AutoRefresh,
            isReverseIterator: true,
            includeDeletedRecords,
            includeMutableSegment: includeMutableSegment,
            includeDiskSegment: true,
            includeBottomSegments: true);

        if (iteratorType == IteratorType.Snapshot)
        {
            MoveMutableSegmentForward();
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
            includeMutableSegment: false,
            includeDiskSegment: false,
            includeBottomSegments: false);
        return iterator;
    }

    /// <summary>
    /// Creates an iterator that enables scanning of the in-memory segments.
    /// This includes read-only segments and mutable segment.
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
            includeMutableSegment: true,
            includeDiskSegment: false,
            includeBottomSegments: false);
        return iterator;
    }
}
