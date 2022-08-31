using Tenray.ZoneTree.Segments;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree;

/// <summary>
/// The interface for the maintenance of a ZoneTree.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public interface IZoneTreeMaintenance<TKey, TValue>
{
    /// <summary>
    /// Gets current mutable segment.
    /// Mutable segment is the only writable part of the LSM tree.
    /// </summary>
    IMutableSegment<TKey, TValue> MutableSegment { get; }

    /// <summary>
    /// Gets current readonly segments in-memory.
    /// MoveMutableSegmentForward operation moves writable segment to the read-only
    /// segments layer.
    /// The readonly segments remains in memory until merge operation done.
    /// </summary>
    IReadOnlyList<IReadOnlySegment<TKey, TValue>> ReadOnlySegments { get; }

    /// <summary>
    /// Gets current disk segment.
    /// </summary>
    IDiskSegment<TKey, TValue> DiskSegment { get; }

    /// <summary>
    /// Gets current bottom segments.
    /// </summary>
    IReadOnlyList<IDiskSegment<TKey, TValue>> BottomSegments { get; }

    /// <summary>
    /// Retrieves the number of read only segments.
    /// </summary>
    int ReadOnlySegmentsCount { get; }

    /// <summary>
    /// Retrieves the number of records in read-only segments.
    /// </summary>
    long ReadOnlySegmentsRecordCount { get; }

    /// <summary>
    /// Retrieves the number of records in mutable segment.
    /// </summary>
    long MutableSegmentRecordCount { get; }

    /// <summary>
    /// Retrieves the total number of records that lies in memory
    /// excluding the sparse array records of DiskSegment.
    /// In an LSM tree, records can be duplicated across different segments.
    /// </summary>
    long InMemoryRecordCount { get; }

    /// <summary>
    /// Retrieves the total number of records that lies in memory and disk
    /// excluding the sparse array records of DiskSegments.
    /// In an LSM tree, records can be duplicated across different segments.
    /// Hence, this is not the actual unique record count of the tree.
    /// To get exact record count, a partial database scan is needed.
    /// Use Count() and CountFullScan() for actual record count.
    /// </summary>
    long TotalRecordCount { get; }

    /// <summary>
    /// true if merge operation is running, otherwise false.
    /// </summary>
    bool IsMerging { get; }

    /// <summary>
    /// true if bottom segments merge operation is running, otherwise false.
    /// </summary>
    bool IsBottomSegmentsMerging { get; }

    /// <summary>
    /// Moves mutable segment into readonly segment.
    /// This will clear the writable region of the LSM tree.
    /// This method is thread safe and can be called from many threads.
    /// </summary>
    void MoveMutableSegmentForward();

    /// <summary>
    /// Merges available in-memory read-only segments to the disk segment.
    /// </summary>
    Thread StartMergeOperation();

    /// <summary>
    /// Merges selected bottom segments into a single bottom disk segment.
    /// </summary>
    /// <param name="from">The lower bound</param>
    /// <param name="to">The upper bound</param>
    /// <returns></returns>
    Thread StartBottomSegmentsMergeOperation(int from, int to);

    /// <summary>
    /// Attempts to cancel merge operation.
    /// </summary>
    void TryCancelMergeOperation();

    /// <summary>
    /// Attempts to cancel bottom segments merge operation.
    /// </summary>
    void TryCancelBottomSegmentsMergeOperation();

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
    /// Event is fired when mutable segment is moved forward.
    /// </summary>
    event MutableSegmentMovedForward<TKey, TValue> OnMutableSegmentMovedForward;

    /// <summary>
    /// Event is fired when merge operation is started.
    /// </summary>
    event MergeOperationStarted<TKey, TValue> OnMergeOperationStarted;

    /// <summary>
    /// Event is fired when merge operation is completed.
    /// </summary>
    event MergeOperationEnded<TKey, TValue> OnMergeOperationEnded;

    /// <summary>
    /// Event is fired when bottom segments merge operation is completed.
    /// </summary>
    public event BottomSegmentsMergeOperationStarted<TKey, TValue> OnBottomSegmentsMergeOperationStarted;

    /// <summary>
    /// Event is fired when bottom segments merge operation is completed.
    /// </summary>
    public event BottomSegmentsMergeOperationEnded<TKey, TValue> OnBottomSegmentsMergeOperationEnded;

    /// <summary>
    /// Event is fired when the new disk segment is created.
    /// This is the best moment to initialize a sparse array.
    /// SparseArray is an in memory array that reduces disk reads.
    /// It is the best practice to call DiskSegment.InitSparseArray()
    /// when this event is fired.
    /// </summary>
    event DiskSegmentCreated<TKey, TValue> OnDiskSegmentCreated;

    /// <summary>
    /// Event is fired when the new disk segment is activated.
    /// </summary>
    event DiskSegmentCreated<TKey, TValue> OnDiskSegmentActivated;

    /// <summary>
    /// Event is fired when a write ahead log cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropReadOnlySegment<TKey, TValue> OnCanNotDropReadOnlySegment;

    /// <summary>
    /// Event is fired when a disk segment cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropDiskSegment<TKey, TValue> OnCanNotDropDiskSegment;

    /// <summary>
    /// Event is fired when a disk segment creator cannot be dropped.
    /// This does not harm the database consistency.
    /// The cleanup task can be done later.
    /// </summary>
    event CanNotDropDiskSegmentCreator<TKey, TValue> OnCanNotDropDiskSegmentCreator;

    /// <summary>
    /// Event is fired when the ZoneTree is disposing.
    /// </summary>
    event ZoneTreeIsDisposing<TKey, TValue> OnZoneTreeIsDisposing;
}

/// <summary>
/// Event is fired when mutable segment is moved forward.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
public delegate void MutableSegmentMovedForward<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);

/// <summary>
/// Event is fired when the merge operation is started.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
public delegate void MergeOperationStarted<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);

/// <summary>
/// Event is fired when the merge operation is ended.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
/// <param name="mergeResult">The merge operation result</param>
public delegate void MergeOperationEnded<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, MergeResult mergeResult);


/// <summary>
/// Event is fired when the bottom segments merge operation is started.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
public delegate void BottomSegmentsMergeOperationStarted<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);

/// <summary>
/// Event is fired when the bottom segments merge operation is ended.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
/// <param name="mergeResult">The merge operation result</param>
public delegate void BottomSegmentsMergeOperationEnded<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, MergeResult mergeResult);

/// <summary>
/// Event is fired when the disk segment is created.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
/// <param name="newDiskSegment">The new disk segment</param>
/// <param name="isBottomSegment">True if the bottom disk segment is created, otherwise false.</param>
public delegate void DiskSegmentCreated<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment, bool isBottomSegment);

/// <summary>
/// Event is fired when the disk segment is activated.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree maintenance</param>
/// <param name="newDiskSegment">The new disk segment</param>
/// /// <param name="isBottomSegment">True if the bottom disk segment is activated, otherwise false.</param>
public delegate void DiskSegmentActivated<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment, bool isBottomSegment);

/// <summary>
/// Event is fired when the read-only segment can not be dropped.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="readOnlySegment">The read-only segment</param>
/// <param name="e">The exception</param>
public delegate void CanNotDropReadOnlySegment<TKey, TValue>
    (IReadOnlySegment<TKey, TValue> readOnlySegment, Exception e);

/// <summary>
/// Event is fired when the disk segment can not be dropped.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="diskSegment">The disk segment</param>
/// <param name="e">The exception</param>
public delegate void CanNotDropDiskSegment<TKey, TValue>
    (IDiskSegment<TKey, TValue> diskSegment, Exception e);

/// <summary>
/// Event is fired when the disk segment creator can not be dropped.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="diskSegmentCreator">The disk segment creator</param>
/// <param name="e">The exception</param>
public delegate void CanNotDropDiskSegmentCreator<TKey, TValue>
    (IDiskSegmentCreator<TKey, TValue> diskSegmentCreator, Exception e);

/// <summary>
/// Event is fired when the ZoneTree is disposing.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="zoneTree">The ZoneTree</param>
public delegate void ZoneTreeIsDisposing<TKey, TValue>
    (IZoneTreeMaintenance<TKey, TValue> zoneTree);
