# Read-Path Caching

ZoneTree's disk read path uses multiple cache layers to reduce repeated disk work.

## Circular Key And Value Caches

Disk segment options include circular caches for keys and values:

* `KeyCacheSize`
* `ValueCacheSize`
* `KeyCacheRecordLifeTimeInMillisecond`
* `ValueCacheRecordLifeTimeInMillisecond`

These caches are checked before deeper block access during lookups and searches. They are useful when recent keys or values are repeatedly touched.

Larger caches can reduce repeated reads but use more memory. Smaller caches keep memory lower but may increase disk/block access.

## Block Cache

Block cache behavior matters when data is read from disk segments, especially with compressed disk blocks.

Repeated reads over nearby keys can benefit from cached blocks. Random reads over a very large keyspace may benefit less unless the working set fits cache.

## Iterator Cache Contribution

Some iterator paths can choose whether disk segment reads contribute to the block cache.

Use cache contribution when scans represent a useful working set that will be read again. Avoid it for one-off large scans that would evict more valuable cached blocks.

## Cache Lifetime

Record lifetime options help keep circular caches from holding stale entries longer than intended.

Tune cache lifetime based on access pattern:

* short lifetime for highly variable random reads,
* longer lifetime for repeated hot-key access,
* smaller caches when memory budget is tight.

## Diagnostics

If reads slow down, inspect:

* key layout,
* segment count,
* compression settings,
* sparse array step size,
* cache sizes,
* iterator behavior,
* storage latency.
