# Backups

Backup strategy depends on the WAL mode, maintenance behavior, and whether your application can reconstruct data.

## Basic Rule

Back up the full ZoneTree data directory as a unit. It contains metadata, WAL files, and segment files that together define the database.

Do not copy one piece of the storage directory and assume it is enough.

## Simple Full Backup

The simplest reliable shape is:

* stop or pause writes,
* wait for background maintenance if you need a quiet storage shape,
* dispose the tree or keep it closed during the copy,
* copy the full data directory,
* restore into a separate directory and open it as a test.

For many embedded applications, this is the easiest operational model.

## Maintenance-Aware Backup

Maintenance changes the physical storage shape by moving and merging segments. Coordinate backups so the copied state is consistent with metadata, WAL files, and segment files.

For a stricter application-level backup window:

* pause writes,
* call `maintainer.EvictToDisk()` if you want to move current in-memory data toward disk,
* call `maintainer.WaitForBackgroundThreads()`,
* call `zoneTree.Maintenance.SaveMetaData()` if you want the JSON metadata file to be current for inspection,
* copy the full data directory,
* resume writes.

`SaveMetaData` is useful for making the JSON metadata current, but the metadata file alone is not the database.

## Incremental WAL Backup

ZoneTree has WAL-related support for incremental backup scenarios through `WriteAheadLogOptions.EnableIncrementalBackup`.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .ConfigureWriteAheadLogOptions(options =>
    {
        options.EnableIncrementalBackup = true;
    })
    .OpenOrCreate();
```

When a WAL is compacted or replaced, ZoneTree appends the previous WAL content to an incremental `.full` file beside that WAL. This preserves WAL history across WAL compaction; it is not a replacement for copying the full database directory.

If you use incremental backup, test restore end to end. Backup is only real when restore has been verified.

## Rebuildable Data

Some ZoneTree deployments store indexes, caches, or derived views that can be rebuilt from another source. In those systems, backup may focus on the source of truth instead of the ZoneTree directory.

Use `No WAL` only when that data-loss boundary is intentional.
