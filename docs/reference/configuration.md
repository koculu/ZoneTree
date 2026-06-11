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

Comparer semantics are part of the persisted keyspace. ZoneTree stores the comparer type in metadata and validates it on open, but a custom comparer with the same type can still become incompatible if its comparison behavior changes. Create a new ZoneTree and rebuild/copy data when you need a different order.

## Default Profile

ZoneTree defaults are designed as a practical general-purpose profile. Start with them, then tune after measuring the actual workload.

| Area | Default |
| --- | --- |
| Mutable segment max item count | `1_000_000` records |
| Disk segment max item count | `20_000_000` records |
| BTree lock mode | `NodeLevelMonitor` |
| BTree node size | `128` |
| BTree leaf size | `128` |
| WAL mode | `AsyncCompressed` |
| WAL compression block size | `256 KB` |
| WAL compression | `LZ4`, fastest level |
| Async compressed WAL empty queue poll interval | `100 ms` |
| Sync compressed WAL tail writer | enabled |
| Sync compressed WAL tail writer interval | `500 ms` |
| Disk segment mode | `MultiPartDiskSegment` |
| Disk segment compression block size | `4 MB` |
| Disk segment compression | `LZ4`, fastest level |
| Multipart minimum record count | `1_500_000` records |
| Multipart maximum record count | `3_000_000` records |
| Key cache size | `1024` records |
| Value cache size | `1024` records |
| Key cache lifetime | `10 seconds` |
| Value cache lifetime | `10 seconds` |
| Default sparse array step size | `1024` |
| Maintainer maximum read-only segment count | `64` |
| Maintainer merge record threshold | `0` records |
| Maintainer block cache lifetime | `1 minute` |
| Maintainer inactive cache cleanup interval | `30 seconds` |
| Maintainer inactive cache cleanup job from `CreateMaintainer()` | enabled |
| Live backup after normal merge | enabled |
| Live backup in-memory records | enabled |
| Live backup in-memory mode | `Live` |
| Live backup file transfer concurrency | `8` |
| Live backup record batch compression | `LZ4`, fastest level |
| Live backup record batch compression block size | `1 MB` |
| Console logger level | `Warning` |

## Memory

`MutableSegmentMaxItemCount` controls when the active mutable segment is moved forward.

The default is `1_000_000` records. This is a good starting point for small keys and values. Lower it for large values. Raise it only when memory budget and maintenance behavior are understood.

## Disk

Disk segment options affect file layout, compression, circular key/value caches, sparse arrays, multipart sizing, and merge behavior.

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

The default disk profile uses multipart disk segments, `20_000_000` as the disk segment max item count, `1_500_000` to `3_000_000` records per multipart part, `4 MB` disk compression blocks, LZ4 fastest compression, `1024` sparse array step size, and `1024` key/value cache entries with `10 second` lifetimes.

The decompressed block cache is not configured by `DiskSegmentOptions`. Disk compression block size is configured here, but inactive decompressed block cleanup is controlled by the maintainer.

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

The default WAL profile uses async compressed WAL, `256 KB` compression blocks, LZ4 fastest compression, and a `100 ms` async empty-queue poll interval.

Incremental backup is disabled by default. Enable it only when you intentionally need WAL history preserved during WAL replacement or compaction.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .ConfigureWriteAheadLogOptions(options =>
    {
        options.EnableIncrementalBackup = true;
    })
    .OpenOrCreate();
```

## Live Backup

Live backup is configured with `LiveBackupOptions`.

Important options:

| Option | Purpose |
| --- | --- |
| `Store` | backup destination implementation |
| `BackupAfterMerge` | requests a generation after successful normal merges |
| `Schedule` | optional UTC schedule for automatic generations |
| `IncludeInMemoryRecords` | streams mutable/read-only in-memory records into the generation |
| `InMemoryMode` | chooses live or snapshot in-memory collection |
| `RecordBatchCompression` | compression profile for in-memory record batches |
| `MaxConcurrentFileTransfers` | concurrent disk segment file uploads |

The local implementation is configured with `LocalLiveBackupOptions`:

| Option | Purpose |
| --- | --- |
| `Directory` | local backup root directory |
| `CopyBufferSize` | buffer size used for file copy operations |
| `KeepLastGenerations` | optional local retention policy |

Live backup is exposed for built-in non-transactional ZoneTree instances. Transactional trees need a transaction-aware backup design that captures transaction-log state together with storage-engine state.

## Maintenance

The maintainer controls background merge work and inactive cache cleanup. Inactive cache cleanup releases decompressed disk blocks and expired circular key/value cache records.

The maintainer created by `zoneTree.CreateMaintainer()` uses these defaults:

| Option | Default |
| --- | --- |
| `MaximumReadOnlySegmentCount` | `64` |
| `ThresholdForMergeOperationStart` | `0` records |
| `BlockCacheLifeTime` | `1 minute` |
| `InactiveBlockCacheCleanupInterval` | `30 seconds` |
| inactive-cache cleanup job | enabled |

The normal `CreateMaintainer()` path starts the cleanup job by default. Longer `BlockCacheLifeTime` can improve repeated disk reads but retains more decompressed blocks in memory.

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
