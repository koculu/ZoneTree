#undef TRACE_ENABLED

using System.Collections.Concurrent;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

/// <summary>
/// The maintainer for ZoneTree to control merge operations and memory compaction.
/// </summary>
/// <remarks>
/// You must complete all pending tasks of this maintainer before disposing associated ZoneTree.
/// You should not dispose ZoneTree before disposing the maintainer.
/// </remarks>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public sealed class BasicZoneTreeMaintainer<TKey, TValue> : IMaintainer, IDisposable
{
    readonly ILogger Logger;

    volatile bool RestartMerge;

    readonly CancellationTokenSource PeriodicTimerCancellationTokenSource = new();

    /// <summary>
    /// The associated ZoneTree instance.
    /// </summary>
    public IZoneTree<TKey, TValue> ZoneTree { get; }

    /// <summary>
    /// The associated ZoneTree maintenance instance.
    /// </summary>
    public IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    /// <summary>
    /// Minimum sparse array length when a new disk segment is created.
    /// Default value is 0.
    /// </summary>
    public int MinimumSparseArrayLength { get; set; } = 0;

    /// <summary>
    /// Configures sparse array step length when the disk segment length is bigger than
    /// MinimumSparseArrayLength * SparseArrayStepLength.
    /// The default value is 1000.
    /// <remarks>The sparse array length reduce binary lookup range on disk segment
    /// to reduce IO.
    /// </remarks>
    /// </summary>
    public int SparseArrayStepLength { get; set; } = 1_000;

    /// <summary>
    /// Starts merge operation when records count
    /// in read-only segments exceeds this value.
    /// Default value is 2M.
    /// </summary>
    public int ThresholdForMergeOperationStart { get; set; } = 2_000_000;

    /// <summary>
    /// Starts merge operation when read-only segments
    /// count exceeds this value.
    /// Default value is 64.
    /// </summary>
    public int MaximumReadOnlySegmentCount { get; set; } = 64;

    /// <summary>
    /// Enables a periodic timer to release disk segment unused block cache.
    /// </summary>
    public bool EnablePeriodicTimer { get; set; } = true;

    /// <summary>
    /// Sets or gets Disk Segment block cache life time.
    /// </summary>
    public long DiskSegmentBufferLifeTime { get; set; } = TimeSpan.FromSeconds(10).Ticks;

    /// <summary>
    /// Sets or gets Periodic timer interval.
    /// </summary>
    public TimeSpan PeriodicTimerInterval { get; set; } = TimeSpan.FromSeconds(5);

    readonly ConcurrentDictionary<int, Thread> MergerThreads = new();

    /// <summary>
    /// Creates a BasicZoneTreeMaintainer.
    /// </summary>
    /// <param name="zoneTree">The ZoneTree</param>
    /// <param name="logger">The logger</param>
    public BasicZoneTreeMaintainer(IZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
    {
        Logger = logger ?? zoneTree.Logger;
        ZoneTree = zoneTree;
        Maintenance = zoneTree.Maintenance;
        AttachEvents();
        if (EnablePeriodicTimer)
            Task.Run(StartPeriodicTimer);
    }

    /// <summary>
    /// Creates a BasicZoneTreeMaintainer.
    /// </summary>
    /// <param name="zoneTree">The transactional ZoneTree</param>
    /// <param name="logger">The logger</param>
    public BasicZoneTreeMaintainer(ITransactionalZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
    {
        Logger = logger ?? zoneTree.Logger;
        ZoneTree = zoneTree.Maintenance.ZoneTree;
        Maintenance = ZoneTree.Maintenance;
        AttachEvents();
        Task.Run(StartPeriodicTimer);
    }

    void AttachEvents()
    {
        Maintenance.OnSegmentZeroMovedForward += OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated += OnDiskSegmentCreated;
        Maintenance.OnMergeOperationEnded += OnMergeOperationEnded;
        Maintenance.OnZoneTreeIsDisposing += OnZoneTreeIsDisposing;
    }

    void OnZoneTreeIsDisposing(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        Logger.LogTrace("ZoneTree is disposing. BasicZoneTreeMaintainer disposal started.");
        PeriodicTimerCancellationTokenSource.Cancel();
        TryCancelRunningTasks();
        CompleteRunningTasks();
        Dispose();
        Logger.LogTrace("BasicZoneTreeMaintainer is disposed.");
    }

    void OnMergeOperationEnded(
        IZoneTreeMaintenance<TKey, TValue> zoneTree,
        MergeResult mergeResult)
    {
        switch (mergeResult)
        {
            case MergeResult.RETRY_READONLY_SEGMENTS_ARE_NOT_READY:
                Trace("RETRY_READONLY_SEGMENTS_ARE_NOT_READY");
                RestartMerge = false;
                StartMerge();
                break;
            case MergeResult.ANOTHER_MERGE_IS_RUNNING:
                Trace("ANOTHER_MERGE_IS_RUNNING");
                RestartMerge = true;
                break;
            case MergeResult.NOTHING_TO_MERGE:
                Trace("NOTHING_TO_MERGE");
                break;
            case MergeResult.CANCELLED_BY_USER:
                Trace("CANCELLED_BY_USER");
                break;
            case MergeResult.SUCCESS:
                Trace("MERGE SUCCESS");
                if (RestartMerge)
                {
                    Trace("Restarting merge");
                    RestartMerge = false;
                    StartMerge();
                }
                break;
            case MergeResult.FAILURE:
                Trace("MERGE FAILURE");
                break;
        }
        MergerThreads.Remove(Environment.CurrentManagedThreadId, out _);
    }

    void OnDiskSegmentCreated(IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment)
    {
        var sparseArraySize = newDiskSegment.Length / SparseArrayStepLength;
        newDiskSegment.InitSparseArray(Math.Min(MinimumSparseArrayLength, sparseArraySize));
    }

    void OnSegmentZeroMovedForward(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        if (Maintenance.ReadOnlySegmentsCount > MaximumReadOnlySegmentCount)
            StartMerge();
        else if (Maintenance.ReadOnlySegmentsRecordCount > ThresholdForMergeOperationStart)
            StartMerge();
    }

    void StartMerge()
    {
        lock (this)
        {
            var mergerThread = Maintenance.StartMergeOperation();
            if (mergerThread == null)
                return;
            MergerThreads.AddOrUpdate(
                mergerThread.ManagedThreadId,
                mergerThread,
                (key, value) => mergerThread);
        }
    }

    /// <summary>
    /// Cancels active merge operation if possible.
    /// </summary>
    public void TryCancelMergeOperation()
    {
        Maintenance.TryCancelMergeOperation();
    }

    public void TryCancelRunningTasks()
    {
        TryCancelMergeOperation();
    }

    /// <summary>
    /// Waits until all merge tasks are completed.
    /// </summary>
    public void CompleteRunningTasks()
    {
        while (true)
        {
            var threads = MergerThreads.ToArray();
            if (threads.Length == 0)
                return;
            Trace($"Waiting {threads.Length} merge threads");
            foreach (var a in threads)
            {
                var t = a.Value;
                if (t.ThreadState == ThreadState.Stopped)
                    MergerThreads.TryRemove(a.Key, out var _);
                else
                    t.Join();
            }
            Trace("Wait ended");
        }
    }

    async Task StartPeriodicTimer()
    {
        var cts = PeriodicTimerCancellationTokenSource;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(cts.Token))
        {
            var ticks = DateTime.UtcNow.Ticks - DiskSegmentBufferLifeTime;
            var releasedCount = ZoneTree.Maintenance.DiskSegment.ReleaseReadBuffers(ticks);
            Trace("Released Buffers: " + releasedCount);
        }
    }

    void Trace(string msg)
    {
        Logger.LogTrace(msg);
    }

    /// <summary>
    /// Disposes this maintainer.
    /// </summary>
    public void Dispose()
    {
        PeriodicTimerCancellationTokenSource.Cancel();
        Maintenance.OnSegmentZeroMovedForward -= OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated -= OnDiskSegmentCreated;
        Maintenance.OnMergeOperationEnded -= OnMergeOperationEnded;
        Maintenance.OnZoneTreeIsDisposing -= OnZoneTreeIsDisposing;
    }
}