# Backups

ZoneTree supports live backup for ordinary `IZoneTree<TKey, TValue>` instances and offline full backup for any deployment.

Live backup is the preferred shape for long-running applications. It creates complete backup generations while the tree remains open for reads and writes.

## Live Backup Model

A live backup generation is a complete backup unit. It contains:

* immutable disk segment files,
* optional in-memory records streamed into a record batch,
* a generation catalog written by the backup store.

ZoneTree collects the current backup view, pins the disk segments that belong to that view, and lets the store upload or reuse the segment files. In-memory records are streamed through ZoneTree iterators.

This keeps the live system active while a backup generation is being created. Manual generations run in the caller's operation and complete before `CreateGenerationAsync()` returns. Scheduled and merge-triggered generations run asynchronously.

Live backup is not exposed for transactional ZoneTree yet. Transactional backup must capture transaction-log state together with the storage-engine state, so it needs its own design.

## Quick Start

Use `CreateLiveBackup` with a local directory:

```csharp
using ZoneTree.Backup;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .OpenOrCreate();

using var backup = zoneTree.CreateLiveBackup("backup/app");

backup.Start();

try
{
    await backup.CreateGenerationAsync();
}
finally
{
    backup.Stop();
    await backup.WaitForLiveBackupAsync();
}
```

`Start()` activates the live backup coordinator by attaching merge-triggered backup handling and starting the configured scheduler. It does not create a generation by itself unless a schedule fires or a successful merge requests one.

Use `CreateGenerationAsync()` when you want to create a backup generation immediately.

`CreateGenerationAsync()` completes when the generation is finished. If the generation fails, the exception is propagated to the caller, usually as `LiveBackupGenerationException`.

Use `Stop()` to disable automatic live backup triggers. Use `WaitForLiveBackupAsync()` when you want to wait until live backup is fully inactive.

## Start, Stop, And Wait

`Start()` and `Stop()` are synchronous lifecycle methods.

```csharp
backup.Start();
backup.Stop();
```

`Start()` enables automatic live backup activity:

* merge-triggered generations,
* scheduled generations, if a schedule is configured.

`Stop()` disables automatic live backup activity:

* merge-triggered generation handling is detached,
* scheduler cancellation is requested.

`Stop()` does not wait for the scheduler to exit or for an active generation to finish. To wait until live backup activity is fully inactive, call:

```csharp
await backup.WaitForLiveBackupAsync();
```

For non-async callers:

```csharp
backup.WaitForLiveBackup();
```

`WaitForLiveBackupAsync()` waits while any of the following is true:

* live backup is still started,
* the scheduler is still running,
* a backup generation is still running.

This means `WaitForLiveBackupAsync()` normally returns after `Stop()` has been called and all background activity has completed.

```csharp
backup.Stop();
await backup.WaitForLiveBackupAsync();
```

`Dispose()` calls `Stop()` and waits for live backup activity to finish. This makes `using var backup = ...` safe during shutdown.

## Manual Generations

To create a generation manually, use:

```csharp
await backup.CreateGenerationAsync();
```

For non-async callers:

```csharp
backup.CreateGeneration();
```

Manual generations do not require `Start()`.

This is valid:

```csharp
using var backup = zoneTree.CreateLiveBackup("backup/app");

await backup.CreateGenerationAsync();
```

A manual generation runs directly in the caller's operation. If the generation fails, the exception is thrown to the caller.

## Generation Concurrency

Live backup does not queue generations. ZoneTree allows only one live backup generation at a time.

Manual generation requests are strict. If another generation is already running, `CreateGenerationAsync()` fails instead of queueing another generation.

Scheduled and merge-triggered generation requests are opportunistic. If a generation is already running, the scheduled or merge-triggered request is skipped silently.

In short:

```text
manual CreateGenerationAsync() while busy  -> throws
scheduled generation while busy            -> skipped
merge-triggered generation while busy      -> skipped
```

This avoids stale backup work and prevents scheduled or merge-triggered generations from building a backlog.

## Local Store

`LocalLiveBackupProvider` writes generations into a local directory.

```csharp
using var backup = zoneTree.CreateLiveBackup(new LiveBackupOptions
{
    Store = new LocalLiveBackupProvider("backup/app")
});
```

The local implementation stores:

* `manifest.json`: latest completed generation id,
* `generations/*.json`: generation catalogs,
* `data/*`: copied disk segment files,
* `records/*.bin`: in-memory record batches.

Disk segment files are immutable, so the store can reuse files that were already copied by earlier generations.

The local backup directory is shaped like this:

```text
backup/app/
├── manifest.json
├── data/
│   ├── <segment-file>
│   └── ...
├── records/
│   ├── 00000000000000000001.bin
│   └── ...
└── generations/
    ├── 00000000000000000001.json
    └── ...
```

Generation catalogs and record batches use zero-padded numeric names so lexical filename ordering matches numeric generation or batch ordering.

## In-Memory Records

By default, live backup includes in-memory records:

```csharp
IncludeInMemoryRecords = true
```

These records are serialized with the ZoneTree key and value serializers. Record batches are compressed by default with LZ4.

Use `Live` mode when you want the least intrusive backup pass:

```csharp
InMemoryMode = LiveBackupInMemoryMode.Live
```

`Live` mode iterates mutable and read-only in-memory segments without moving the mutable segment forward.

Use `Snapshot` mode when you want a cleaner in-memory boundary:

```csharp
InMemoryMode = LiveBackupInMemoryMode.Snapshot
```

`Snapshot` mode moves the mutable segment forward and then iterates read-only in-memory segments.

If in-memory records are disabled, a generation contains disk segments only. Recent writes become part of later generations after maintenance moves them to disk.

## Merge-Triggered Generations

By default, successful normal merges request a new live backup generation:

```csharp
BackupAfterMerge = true
```

This is useful because a normal merge can move recent in-memory data into immutable disk segments.

Bottom-segment merges do not request live backup generations. They reshape already durable bottom data and do not move recent mutable data into disk coverage.

If a backup generation is already running, merge-triggered generation requests are skipped. Live backup does not queue multiple generations behind the active one.

Disable merge-triggered generations when you want only manual or scheduled backup:

```csharp
BackupAfterMerge = false
```

Merge-triggered generations run asynchronously. If a merge-triggered generation starts and then fails, the failure is logged through the ZoneTree logger.

## Scheduling

Live backup can create generations on a UTC schedule.

```csharp
using var backup = zoneTree.CreateLiveBackup(new LiveBackupOptions
{
    Store = new LocalLiveBackupProvider("backup/app"),
    Schedule = LiveBackupSchedule.Daily(
        new TimeOnly(2, 0),
        new TimeOnly(14, 0))
});

backup.Start();
```

Available schedules:

```csharp
LiveBackupSchedule.Every(TimeSpan.FromMinutes(15));

LiveBackupSchedule.Daily(
    new TimeOnly(2, 0),
    new TimeOnly(14, 0));

LiveBackupSchedule.Weekly(
    LiveBackupSchedule.On(DayOfWeek.Sunday, new TimeOnly(2, 0)));
```

If a scheduled generation is due while another backup generation is already running, ZoneTree skips that tick. Backup scheduling does not build a backlog.

Scheduled generations run asynchronously. If a scheduled generation starts and then fails, the failure is logged through the ZoneTree logger.

To stop the scheduler and wait until it has exited:

```csharp
backup.Stop();
await backup.WaitForLiveBackupAsync();
```

## Shutdown

A typical shutdown sequence is:

```csharp
backup.Stop();
await backup.WaitForLiveBackupAsync();
```

`Stop()` disables new scheduled and merge-triggered generations. `WaitForLiveBackupAsync()` waits until the scheduler has exited and any active generation has completed.

`Stop()` does not create a final generation by itself. If you want a final backup point before shutdown, create it explicitly:

```csharp
await backup.CreateGenerationAsync();

backup.Stop();
await backup.WaitForLiveBackupAsync();
```

For non-async callers:

```csharp
backup.CreateGeneration();

backup.Stop();
backup.WaitForLiveBackup();
```

If `LiveBackup` is disposed, it stops live backup and waits for live backup activity to finish.

## Retention

Local retention is configured on `LocalLiveBackupOptions`:

```csharp
using var backup = zoneTree.CreateLiveBackup(new LiveBackupOptions
{
    Store = new LocalLiveBackupProvider(new LocalLiveBackupOptions
    {
        Directory = "backup/app",
        KeepLastGenerations = 7
    })
});
```

The local store keeps the latest completed generations and removes files that are no longer referenced by retained generation catalogs.

Retention is applied at the backup-path level. If a retained generation reuses a segment file that was originally copied by an older generation, the physical segment file is preserved because the retained generation still references the same backup path.

Custom stores own their own retention policy.

## Custom Stores And Sources

Implement `ILiveBackupStore` when backup data should go somewhere other than a local directory.

Store implementations decide:

* how generation ids are assigned,
* where segment files are stored,
* whether an existing segment file can be reused,
* how record batches are written,
* how retention works.

ZoneTree decides:

* which segments belong to a generation,
* when disk segments must be pinned,
* how in-memory records are streamed,
* when manual, scheduled, and merge-triggered generations are started or skipped.

ZoneTree allows only one live backup generation at a time. Manual generation requests fail if another generation is already running. Scheduled and merge-triggered generation requests are skipped while a generation is active.

Segment reuse is handled by `UseSegmentAsync`:

```csharp
Task<bool> UseSegmentAsync(
    long generationId,
    DiskSegmentFile file,
    CancellationToken cancellationToken);
```

Return `false` when the store already has the segment file. Return `true` when ZoneTree should upload the file through `UploadSegmentFileAsync`.

Restore uses the matching read-side abstraction:

```csharp
public interface ILiveBackupSource
```

The local implementation supports both sides, so restoring the latest local generation is direct:

```csharp
using var restored = await new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/restore")
    .RestoreFromLatestLiveBackup("backup/app");
```

## Offline Full Backup

Offline backup is the simple maintenance-window option:

* stop writes,
* wait for maintenance work to finish,
* dispose the tree,
* copy the full ZoneTree data directory,
* copy the WAL directory too if it is configured separately.

A ZoneTree database is a set of files, not a single file. Depending on configuration, storage can include metadata, disk segments, WAL files, meta WAL files, transaction WAL files, and multi-part segment files.

If `SetWriteAheadLogDirectory(...)` points WAL files outside the data directory, copy both locations from the same backup point.

## Rebuildable Data

Some applications use ZoneTree for indexes, projections, caches, or derived views. If the data can be rebuilt from another source of truth, backup can focus on that source instead of the ZoneTree files.

Use no-WAL or relaxed backup policies only when that data-loss boundary is intentional.
