# Configuration

This page summarizes the most important configuration areas.

## Factory

`ZoneTreeFactory<TKey, TValue>` configures and opens a tree.

Common methods:

* `SetDataDirectory`
* `SetWriteAheadLogDirectory`
* `SetLogger`
* `SetLogLevel`
* `SetComparer`
* `SetKeySerializer`
* `SetValueSerializer`
* `SetMutableSegmentMaxItemCount`
* `SetDiskSegmentMaxItemCount`
* `SetDiskSegmentCompressionBlockSize`
* `SetRandomAccessDeviceManager`
* `SetWriteAheadLogProvider`
* `SetTransactionLog`
* `SetIsDeletedDelegate`
* `SetMarkValueDeletedDelegate`
* `DisableDeletion`
* `Configure`
* `ConfigureWriteAheadLogOptions`
* `ConfigureDiskSegmentOptions`
* `ConfigureTransactionLog`
* `OpenOrCreate`
* `Open`
* `Create`
* `OpenOrCreateTransactional`

Set serializers, comparers, deletion delegates, and storage providers before opening the tree. Serializers cannot be changed after the WAL provider or transaction log has been initialized.

## Memory

`MutableSegmentMaxItemCount` controls when the active mutable segment is moved forward.

Lower this for large values. Raise it only when memory budget and maintenance behavior are understood.

## Disk

Disk segment options affect file layout, compression, caches, sparse arrays, multipart sizing, and merge behavior.

Tune disk options with the actual read/write pattern.

Important options:

| Option | Purpose |
| --- | --- |
| `DiskSegmentMode` | single or multipart disk segment shape |
| `CompressionBlockSize` | block size for compressed random-access disk data |
| `CompressionMethod` | disk segment compression method |
| `CompressionLevel` | compression level for the selected method |
| `MinimumRecordCount` | lower target part size for multipart disk segments |
| `MaximumRecordCount` | upper target part size for multipart disk segments |
| `DefaultSparseArrayStepSize` | sparse index density for disk search |
| `KeyCacheSize` | circular cache size for recently read keys |
| `ValueCacheSize` | circular cache size for recently read values |
| `KeyCacheRecordLifeTimeInMillisecond` | key cache record lifetime |
| `ValueCacheRecordLifeTimeInMillisecond` | value cache record lifetime |

## WAL

WAL options control durability, compression, and backup behavior.

The default WAL mode is `WriteAheadLogMode.AsyncCompressed`. It is the recommended starting point for most persistent databases because it keeps WAL protection enabled while preserving high write throughput.

```csharp
using ZoneTree.Options;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .ConfigureWriteAheadLogOptions(options =>
    {
        options.WriteAheadLogMode = WriteAheadLogMode.AsyncCompressed;
        options.EnableIncrementalBackup = true;
    })
    .OpenOrCreate();
```

Important WAL options:

| Option | Purpose |
| --- | --- |
| `WriteAheadLogMode` | chooses sync, sync-compressed, async-compressed, or no WAL |
| `CompressionBlockSize` | compressed WAL block size |
| `CompressionMethod` | compression method for compressed WAL modes |
| `CompressionLevel` | compression level for the selected method |
| `SyncCompressedModeOptions` | sync-compressed tail writer options |
| `AsyncCompressedModeOptions` | async writer polling behavior |
| `EnableIncrementalBackup` | preserves WAL content during WAL replacement/compaction |

Use sync modes when the application specifically needs synchronous WAL acknowledgment. Use `No WAL` only for cache, temporary, or intentionally rebuildable data.

## Deletion

Deletion behavior is configured with:

* `SetIsDeletedDelegate`,
* `SetMarkValueDeletedDelegate`,
* `DisableDeletion`.

TTL can be modeled through custom deletion logic.

## Logging

Use `SetLogger` to integrate ZoneTree with your application's logging stack, or `SetLogLevel` to adjust the default console logger.

```csharp
using ZoneTree.Logger;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetLogLevel(LogLevel.Warning)
    .OpenOrCreate();
```

## Storage Providers

`SetRandomAccessDeviceManager` controls disk segment storage. `SetWriteAheadLogProvider` controls WAL storage. The default factory uses local file-system backed providers.

These extension points are advanced. Use them when embedding ZoneTree into a custom storage environment.
