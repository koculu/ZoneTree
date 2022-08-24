using System.Collections.Concurrent;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

/// <summary>
/// The maintainer for ZoneTree to control merge operations and memory compaction.
/// </summary>
/// <remarks>
/// You must complete or cancel all pending tasks of this maintainer
/// before disposing.
/// </remarks>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public sealed class ZoneTreeMaintainer<TKey, TValue> : IMaintainer, IDisposable
{
    readonly ILogger Logger;

    volatile bool RestartMerge;

    readonly CancellationTokenSource PeriodicTimerCancellationTokenSource = new();

    readonly ConcurrentDictionary<int, Thread> MergerThreads = new();

    /// <summary>
    /// The associated ZoneTree instance.
    /// </summary>
    public IZoneTree<TKey, TValue> ZoneTree { get; }

    /// <summary>
    /// The associated ZoneTree maintenance instance.
    /// </summary>
    public IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    /// <inheritdoc/>
    public int MinimumSparseArrayLength { get; set; } = 0;

    /// <inheritdoc/>
    public int SparseArrayStepLength { get; set; } = 1_000;

    /// <inheritdoc/>
    public int ThresholdForMergeOperationStart { get; set; } = 2_000_000;

    /// <inheritdoc/>
    public int MaximumReadOnlySegmentCount { get; set; } = 64;

    /// <inheritdoc/>
    public bool EnablePeriodicTimer { get; set; } = true;

    /// <inheritdoc/>
    public long DiskSegmentBufferLifeTime { get; set; } = TimeSpan.FromSeconds(10).Ticks;

    /// <inheritdoc/>
    public TimeSpan PeriodicTimerInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates a ZoneTreeMaintainer.
    /// </summary>
    /// <param name="zoneTree">The ZoneTree</param>
    /// <param name="logger">The logger</param>
    public ZoneTreeMaintainer(IZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
    {
        Logger = logger ?? zoneTree.Logger;
        ZoneTree = zoneTree;
        Maintenance = zoneTree.Maintenance;
        AttachEvents();
        if (EnablePeriodicTimer)
            Task.Run(StartPeriodicTimer);
    }

    /// <summary>
    /// Creates a ZoneTreeMaintainer.
    /// </summary>
    /// <param name="zoneTree">The transactional ZoneTree</param>
    /// <param name="logger">The logger</param>
    public ZoneTreeMaintainer(ITransactionalZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
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
        Logger.LogTrace("ZoneTree is disposing. ZoneTreeMaintainer disposal started.");
        PeriodicTimerCancellationTokenSource.Cancel();
        CompleteRunningTasks();
        Dispose();
        Logger.LogTrace("ZoneTreeMaintainer is disposed.");
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

    /// <inheritdoc/>
    public void TryCancelRunningTasks()
    {
        Maintenance.TryCancelMergeOperation();
    }

    /// <inheritdoc/>
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