namespace Tenray.ZoneTree;

/// <summary>
/// The maintainer for ZoneTree to control 
/// merge operations and memory compaction.
/// </summary>
/// <remarks>
/// You must complete or cancel all pending threads of this maintainer
/// before disposing.
/// </remarks>
public interface IMaintainer : IDisposable
{
    /// <summary>
    /// Starts merge operation when records count
    /// in read-only segments exceeds this value.
    /// Default value is 0.  
    /// </summary>
    int ThresholdForMergeOperationStart { get; set; }

    /// <summary>
    /// Starts merge operation when read-only segments
    /// count exceeds this value.
    /// Default value is 64.
    /// </summary>
    int MaximumReadOnlySegmentCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a periodic timer is enabled to release
    /// unused block and key/value record caches in the disk segment. 
    /// Changing this property will start or stop the periodic timer accordingly. 
    /// The default value is <c>false</c>.
    /// </summary>
    bool EnableJobForCleaningInactiveCaches { get; set; }

    /// <summary>
    /// Sets or gets Disk Segment block cache life time in milliseconds.
    /// Default value is 10_000 milliseconds.
    /// </summary>
    long DiskSegmentBufferLifeTime { get; set; }

    /// <summary>
    /// Gets or sets the interval for the periodic timer that triggers the cleanup job.
    /// The default value is 5 seconds.
    /// </summary>
    TimeSpan InactiveBlockCacheCleanupInterval { get; set; }

    /// <summary>
    /// Tries cancel background threads.
    /// </summary>
    void TryCancelBackgroundThreads();

    /// <summary>
    /// Blocks the calling thread until all background threads have completed their execution.
    /// </summary>
    void WaitForBackgroundThreads();

    /// <summary>
    /// Asynchronously waits for all background threads to complete.
    /// </summary>
    /// <returns>A task that represents the asynchronous wait operation.</returns>
    Task WaitForBackgroundThreadsAsync();

    /// <summary>
    /// Evicts all in-memory data to disk by moving the mutable segment forward and initiating a merge process.
    /// </summary>
    /// <remarks>
    /// This method is responsible for freeing up memory in the LSM tree by moving data from the mutable in-memory segment to disk storage. 
    /// It first advances the current mutable segment to a new state, ensuring that any data currently in memory is prepared for disk storage. 
    /// Afterward, it starts the merging process, which combines the in-memory data with existing on-disk data to maintain the integrity 
    /// and efficiency of the LSM tree structure.
    /// </remarks>
    void EvictToDisk();

    /// <summary>
    /// Initiates the merge process in a new thread.
    /// </summary>
    void StartMerge();

    /// <summary>
    /// Initiates a merge of selected bottom segments into a single bottom disk segment.
    /// </summary>
    /// <param name="fromIndex">The lower bound</param>
    /// <param name="toIndex">The upper bound</param>
    /// <returns></returns>
    void StartBottomSegmentsMerge(
        int fromIndex = 0, int toIndex = int.MaxValue);
}
