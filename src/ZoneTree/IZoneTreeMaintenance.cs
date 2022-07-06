using Tenray.Segments;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;

namespace Tenray;

public interface IZoneTreeMaintenance<TKey, TValue>
{
    /// <summary>
    /// Gets current Segment Zero.
    /// Segment Zero is the writable part of the LSM tree.
    /// </summary>
    IMutableSegment<TKey, TValue> SegmentZero { get; }

    /// <summary>
    /// Gets current disk segment.
    /// </summary>
    IDiskSegment<TKey, TValue> DiskSegment { get; }

    /// <summary>
    /// Gets current readonly segments.
    /// MoveSegmentZero operation moves writable segment to the read only segments section.
    /// The readonly segments remains in memory until merge operation done.
    /// </summary>
    IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments { get; }

    /// <summary>
    /// Retrieves the number of read only segments.
    /// </summary>
    int ReadOnlySegmentsCount { get; }

    /// <summary>
    /// Retrieves the number of records in read only segments.
    /// </summary>
    int ReadOnlySegmentsRecordCount { get; }

    /// <summary>
    /// Retrieves the total number of records that lies in memory
    /// excluding the sparse array records of DiskSegment.
    /// In an LSM tree, records can be duplicated across different segments.
    /// </summary>
    int InMemoryRecordCount { get; }

    /// <summary>
    /// Retrieves the total number of records that lies in memory and disk
    /// excluding the sparse array records of DiskSegment.
    /// In an LSM tree, records can be duplicated across different segments.
    /// Hence, this is not the actual unique record count of the tree.
    /// To get exact record count, a partial database scan is needed.
    /// </summary>
    int TotalRecordCount { get; }

    /// <summary>
    /// true if merge operation is running, otherwise false.
    /// </summary>
    bool IsMerging { get; }

    /// <summary>
    /// Moves mutable segment into readonly segment.
    /// This will clear the writable region of the LSM tree.
    /// This method is thread safe and can be called from many threads.
    /// </summary>
    void MoveSegmentZeroForward();

    /// <summary>
    /// Merges available readonly segments with bottom segment.
    /// Bottom segment is usually persistance segment. ex: Disk or any IO device.
    /// </summary>
    /// <returns>MergeResult.SUCCESS if merge operation is done;
    /// otherwise, status with reason.</returns>
    ValueTask<MergeResult> StartMergeOperation();

    /// <summary>
    /// Attempts to cancel merge operation.
    /// </summary>
    void TryCancelMergeOperation();

    /// <summary>
    /// Saves tree meta data and clears the meta wal record.
    /// After calling this method, the JSON meta file contains 
    /// up to date tree meta data.
    /// 
    /// Saving meta file helps following:
    /// 1. Reduce the size of meta wal file.
    /// 2. Make Json meta file up to date to analyze parts of the LSM tree.
    /// Because Meta WAL file is not human readable.
    /// 
    /// It is up to user to decide when and how frequently save the meta file.
    /// </summary>
    void SaveMetaData();

    /// <summary>
    /// Destroys the tree, deletes entire data and WAL store or folder.
    /// </summary>
    void DestroyTree();

    /// <summary>
    /// Event is fired when segment zero is moved forward.
    /// </summary>
    event SegmentZeroMovedForward<TKey, TValue> OnSegmentZeroMovedForward;

    /// <summary>
    /// Event is fired when merge operation is started.
    /// </summary>
    event MergeOperationStarted<TKey, TValue> OnMergeOperationStarted;

    /// <summary>
    /// Event is fired when merge operation is completed.
    /// </summary>
    event MergeOperationEnded<TKey, TValue> OnMergeOperationEnded;

    /// <summary>
    /// Event is fired when the new disk segment is created.
    /// This is the best moment to initialize a sparse array.
    /// SparseArray is an in memory array that reduces disk reads.
    /// It is the best practice to call DiskSegment.InitSparseArray()
    /// when this event is fired.
    /// </summary>
    event DiskSegmentCreated<TKey, TValue> OnDiskSegmentCreated;

    /// <summary>
    /// Event is fired when a write ahead log cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropReadOnlySegment<TKey, TValue> OnCanNotDropReadOnlySegment;

    /// <summary>
    /// Event is fired when a write ahead log cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropDiskSegment<TKey, TValue> OnCanNotDropDiskSegment;

    /// <summary>
    /// Event is fired when a write ahead log cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropDiskSegmentCreator<TKey, TValue> OnCanNotDropDiskSegmentCreator;
}

public delegate void SegmentZeroMovedForward<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);

public delegate void MergeOperationStarted<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);

public delegate void MergeOperationEnded<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, MergeResult mergeResult);

public delegate void DiskSegmentCreated<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment);

public delegate void CanNotDropReadOnlySegment<TKey, TValue>
    (IReadOnlySegment<TKey, TValue> readOnlySegment, Exception e);

public delegate void CanNotDropDiskSegment<TKey, TValue>
    (IDiskSegment<TKey, TValue> diskSegment, Exception e);

public delegate void CanNotDropDiskSegmentCreator<TKey, TValue>
    (IDiskSegmentCreator<TKey, TValue> diskSegmentCreator, Exception e);
