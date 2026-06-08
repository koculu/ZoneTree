# Maintenance

ZoneTree uses maintenance to move data through the LSM-tree lifecycle. Maintenance keeps write throughput high while gradually producing optimized disk segments.

## Why Maintenance Matters

Writes enter the mutable segment. When that segment fills, it becomes read-only and a new mutable segment is created. Read-only segments are later merged into disk segments.

Without maintenance, data can accumulate in in-memory read-only segments and memory usage can grow.

## Use The Maintainer

For long-running applications, create a maintainer and keep it alive while the tree is active.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .OpenOrCreate();

using var maintainer = zoneTree.CreateMaintainer();
```

The default maintainer starts a periodic inactive-cache cleanup job and listens to segment lifecycle events. It starts merge work when read-only segments cross configured thresholds.

Useful settings:

```csharp
maintainer.MaximumReadOnlySegmentCount = 32;
maintainer.ThresholdForMergeOperationStart = 500_000;
maintainer.EnableJobForCleaningInactiveCaches = true;
maintainer.BlockCacheLifeTime = TimeSpan.FromMinutes(1);
maintainer.InactiveBlockCacheCleanupInterval = TimeSpan.FromSeconds(30);
```

Before shutdown, wait for background work if needed:

```csharp
maintainer.WaitForBackgroundThreads();
```

To move current in-memory data toward disk on demand:

```csharp
maintainer.EvictToDisk();
maintainer.WaitForBackgroundThreads();
```

## Manual Maintenance

Advanced applications can use the maintenance API directly to move mutable segments and start merge operations.

Manual control is useful when:

* you want maintenance only during specific windows,
* you are building a storage service with its own scheduler,
* you need predictable resource usage,
* you want to coordinate maintenance with backups.

Core maintenance operations include:

* `MoveMutableSegmentForward`,
* `StartMergeOperation`,
* `StartBottomSegmentsMergeOperation`,
* `TryCancelMergeOperation`,
* `TryCancelBottomSegmentsMergeOperation`,
* `SaveMetaData`.

## Memory And Maintenance

Write-side memory depends heavily on how quickly frozen segments are moved to disk. If memory grows during heavy writes, check:

* mutable segment max item count,
* number of read-only segments,
* maintainer activity,
* merge throughput,
* value size.

See [memory usage](../storage/memory-usage.md).

## Iterators Can Pin Segments

Long-lived iterators can keep segments alive. Dispose iterators when scans finish.
