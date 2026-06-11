# Read-Path Caching

ZoneTree's disk read path uses two cache layers:

* decompressed block cache,
* circular key/value caches.

They solve different problems and are controlled by different options.

## Block Cache

Disk segments use compressed random-access devices by default. Data is stored in compressed blocks. When a read touches a block, ZoneTree decompresses that block into memory.

The block cache stores these decompressed blocks so nearby or repeated reads do not have to read and decompress the same compressed block again.

This is the main disk-read cache for compressed disk segments.

The disk compression block size is configured by `DiskSegmentOptions.CompressionBlockSize`. The default is `4 MB`. Larger blocks can improve compression ratio and sequential behavior, but each cached block can retain more memory. Smaller blocks can reduce random-read memory pressure, but may reduce compression efficiency.

## Block Cache Lifetime

Block cache cleanup is controlled by the maintainer, not by `DiskSegmentOptions`.

Relevant maintainer settings:

| Setting | Default |
| --- | ---: |
| `BlockCacheLifeTime` | `1 minute` |
| `InactiveBlockCacheCleanupInterval` | `30 seconds` |
| inactive-cache cleanup job from `CreateMaintainer()` | enabled |

The maintainer periodically releases decompressed blocks that have not been accessed within `BlockCacheLifeTime`.

```csharp
using var maintainer = zoneTree.CreateMaintainer();

maintainer.BlockCacheLifeTime = TimeSpan.FromMinutes(2);
maintainer.InactiveBlockCacheCleanupInterval = TimeSpan.FromSeconds(30);
```

Longer block cache lifetime can improve repeated disk reads, but retains more memory. Shorter lifetime lowers retained memory, but may cause repeated decompression and disk reads.

## Iterator Cache Contribution

Iterators can scan large parts of the keyspace. By default, iterator disk reads do not contribute to the shared block cache.

This prevents one-off scans from filling the block cache with data that may not be read again.

Enable cache contribution when the scan represents a useful working set:

```csharp
using var iterator = zoneTree.CreateIterator(
    contributeToTheBlockCache: true);
```

For one-off full scans, keep cache contribution disabled.

## Circular Key And Value Caches

Disk segment options also include small circular caches for deserialized keys and values:

* `KeyCacheSize`
* `ValueCacheSize`
* `KeyCacheRecordLifeTimeInMillisecond`
* `ValueCacheRecordLifeTimeInMillisecond`

The defaults are `1024` key records, `1024` value records, and `10 second` record lifetimes.

These caches are useful when the same disk record indexes are read repeatedly. They are smaller and more targeted than the block cache. They do not replace block cache behavior.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .ConfigureDiskSegmentOptions(options =>
    {
        options.KeyCacheSize = 4096;
        options.ValueCacheSize = 4096;
        options.KeyCacheRecordLifeTimeInMillisecond = 30_000;
        options.ValueCacheRecordLifeTimeInMillisecond = 30_000;
    })
    .OpenOrCreate();
```

## Practical Tuning

For repeated reads over nearby key ranges, start with block cache behavior:

* keep a maintainer alive,
* tune `BlockCacheLifeTime`,
* tune `CompressionBlockSize` if random-read granularity matters,
* decide whether scans should contribute to the block cache.

For repeated point reads to the same records, consider circular key/value cache sizes and lifetimes.

For memory pressure, first identify which cache is growing:

* decompressed block cache is released by maintainer cleanup,
* circular key/value caches are bounded by disk segment options,
* long-lived iterators can keep segments alive even after merges.

## Diagnostics

If reads slow down, inspect:

* key layout and locality,
* segment count,
* compression block size,
* block cache lifetime,
* sparse array step size,
* iterator cache contribution,
* circular key/value cache sizes,
* storage latency.
