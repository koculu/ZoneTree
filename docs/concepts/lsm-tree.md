# LSM-Tree Model

ZoneTree uses a Log-Structured Merge-tree architecture. This model is optimized for high-throughput writes and ordered reads over persistent data.

## Write Path

New writes enter the mutable segment in memory. When the mutable segment reaches its configured record limit, ZoneTree moves it forward into a read-only in-memory segment. Maintenance later merges read-only segments into disk segments.

The high-level flow is:

```text
Mutable segment
  -> read-only segments
  -> disk segment
  -> bottom segments
```

## Read Path

Reads search from newest to oldest storage layers:

* mutable segment,
* read-only segments,
* active disk segment,
* bottom segments.

This order ensures newer values and deletion markers override older records with the same key.

## Compaction

Compaction removes obsolete records and combines segments into larger persistent structures. This is where old values, overwritten records, and deletion markers can be discarded when it is safe.

ZoneTree's default multipart disk segment mode can reduce compaction write amplification by carrying unchanged disk parts forward during merge instead of rewriting the whole persistent level.

## Why This Works Well

LSM-trees are strong when write throughput matters and data can be compacted over time. They are especially useful for indexes, event-like data, queues, caches, time-series layouts, and systems where many writes eventually become a smaller optimized on-disk representation.

## What To Tune

The most important LSM-tree tuning knobs are:

* mutable segment size,
* maintenance behavior,
* merge thresholds,
* disk segment size,
* compression,
* WAL mode,
* cache behavior.

See [write-heavy workloads](../tuning/write-heavy-workloads.md) and [read-heavy workloads](../tuning/read-heavy-workloads.md).

For the merge model behind multipart disk segments, see [write amplification](../tuning/write-amplification.md).
