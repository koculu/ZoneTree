using Tenray;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;

namespace ZoneTree.Maintainers;

public sealed class BasicZoneTreeMaintainer<TKey, TValue> : IDisposable
{
    public IZoneTree<TKey, TValue> ZoneTree { get; }

    public IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    public int MinimumSparseArrayLength = 1_000_000;

    public int SparseArrayStepLength = 128;

    public int ThresholdForMergeOperationStart = 2_000_000;

    public List<Task<MergeResult>> Tasks = new();

    public BasicZoneTreeMaintainer(IZoneTree<TKey, TValue> zoneTree)
    {
        ZoneTree = zoneTree;
        Maintenance = zoneTree.Maintenance;
        AttachEvents();
    }

    public BasicZoneTreeMaintainer(ITransactionalZoneTree<TKey, TValue> zoneTree)
    {
        ZoneTree = zoneTree.Maintenance.ZoneTree;
        Maintenance = ZoneTree.Maintenance;
        AttachEvents();
    }

    private void AttachEvents()
    {
        Maintenance.OnSegmentZeroMovedForward += OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated += OnDiskSegmentCreated;
    }

    private void OnDiskSegmentCreated(IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment)
    {
        var sparseArraySize = newDiskSegment.Length / SparseArrayStepLength;
        newDiskSegment.InitSparseArray(Math.Min(MinimumSparseArrayLength, sparseArraySize));
    }

    private void OnSegmentZeroMovedForward(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        if (Maintenance.ReadOnlySegmentsRecordCount > ThresholdForMergeOperationStart)
            AddRunningTask(Maintenance.StartMergeOperation().AsTask());
    }

    private void AddRunningTask(Task<MergeResult> task)
    {
        if (task.IsCompleted)
            return;

        lock (Tasks)
        {
            Tasks = Tasks.Where(x => !x.IsCompleted).ToList();
            Tasks.Add(task);
        }
    }

    public async ValueTask CompleteRunningTasks()
    {
        await Task.WhenAll(Tasks);
    }

    public void Dispose()
    {
        Maintenance.OnSegmentZeroMovedForward -= OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated -= OnDiskSegmentCreated;
    }
}
