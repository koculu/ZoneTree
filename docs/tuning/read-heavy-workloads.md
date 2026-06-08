# Read-Heavy Workloads

Read-heavy workloads depend on key layout, cache behavior, segment shape, and iterator lifetime.

## Key Layout

Design keys around the reads you need. Ordered storage is powerful when related records are adjacent.

Examples:

```text
userId:timestamp
tenantId:entityType:entityId
indexName:term:documentId
```

## Cache Behavior

Disk block cache is most effective when the working set is smaller than available memory or reads repeatedly touch nearby key ranges.

Random reads over a huge keyspace rely more heavily on disk and sparse index efficiency.

See [read-path caching](../storage/read-path-caching.md).

## Symptom Guide

| Symptom | Likely pressure | First actions |
| --- | --- | --- |
| Point reads slow down | too many segments, sparse index density, cold disk cache | keep maintenance active; tune `DefaultSparseArrayStepSize`; review cache sizes |
| Range scans disturb hot reads | one-off scans contribute to cache pressure | keep iterator `contributeToTheBlockCache` disabled for one-off scans |
| Repeated hot-key reads hit disk too often | key/value circular caches too small or short-lived | increase `KeyCacheSize`, `ValueCacheSize`, or cache lifetimes |
| Latest-first reads are awkward | key layout or iterator direction is mismatched | use `CreateReverseIterator` or encode descending keys intentionally |
| Scans keep old files alive | long-lived iterators pin segments | dispose iterators promptly and keep scan scopes short |

## Segment Count

Too many segments can increase read amplification. Maintenance and merge behavior help keep the read path efficient.

Disk segment sparse arrays and cache settings can also affect point lookup and seek performance.

## Iterators

Use iterators for range scans instead of many independent point reads.

Dispose iterators promptly so they do not pin segments longer than needed.

## Compression

Compression can help read-heavy workloads if IO is the bottleneck and data compresses well. It can hurt if CPU is the bottleneck.

Benchmark with your real data shape.
