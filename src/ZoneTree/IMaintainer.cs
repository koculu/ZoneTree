namespace Tenray.ZoneTree;

/// <summary>
/// The maintainer for ZoneTree to control 
/// merge operations and memory compaction.
/// </summary>
/// <remarks>
/// You must complete or cancel all pending tasks of this maintainer
/// before disposing.
/// </remarks>
public interface IMaintainer : IDisposable
{
    /// <summary>
    /// Minimum sparse array length when a new disk segment is created.
    /// Default value is 0.
    /// </summary>
    int MinimumSparseArrayLength { get; set; }

    /// <summary>
    /// Configures sparse array step length when the disk segment length is bigger than
    /// MinimumSparseArrayLength * SparseArrayStepLength.
    /// The default value is 1000.
    /// <remarks>The sparse array length reduce binary lookup range on disk segment
    /// to reduce IO.
    /// </remarks>
    /// </summary>
    int SparseArrayStepLength { get; set; }

    /// <summary>
    /// Starts merge operation when records count
    /// in read-only segments exceeds this value.
    /// Default value is 2M.
    /// </summary>
    int ThresholdForMergeOperationStart { get; set; }

    /// <summary>
    /// Starts merge operation when read-only segments
    /// count exceeds this value.
    /// Default value is 64.
    /// </summary>
    int MaximumReadOnlySegmentCount { get; set; }

    /// <summary>
    /// Enables a periodic timer to release disk segment unused block cache.
    /// Changing this property would start or stop the periodic timer.
    /// Default value is true.
    /// </summary>
    bool EnablePeriodicTimer { get; set; }

    /// <summary>
    /// Sets or gets Disk Segment block cache life time in milliseconds.
    /// Default value is 10_000 milliseconds.
    /// </summary>
    long DiskSegmentBufferLifeTime { get; set; }

    /// <summary>
    /// Sets or gets Periodic timer interval.
    /// Default value is 5 seconds;
    /// </summary>
    TimeSpan PeriodicTimerInterval { get; set; }

    /// <summary>
    /// Tries cancel running tasks.
    /// </summary>
    void TryCancelRunningTasks();

    /// <summary>
    /// Waits until all running tasks are completed.
    /// </summary>
    void CompleteRunningTasks();
}
