# Troubleshooting

This page lists common symptoms and where to look first.

## Process Memory Looks High

.NET may keep freed memory available for reuse instead of returning it to the operating system immediately.

Check:

* live managed objects with .NET diagnostics,
* mutable segment size,
* read-only segment count,
* iterator lifetime,
* value size,
* maintenance activity.

See [memory usage](../storage/memory-usage.md).

## Writes Slow Down

Check:

* WAL mode,
* storage latency,
* compression cost,
* mutable segment size,
* maintenance throughput,
* whether transactions or atomic methods are being overused.

## Reads Slow Down

Check:

* segment count,
* cache behavior,
* key layout,
* random vs range access,
* compression CPU cost,
* long-running maintenance.

## Disk Usage Grows

LSM-tree storage can hold obsolete records until compaction removes them.

Check:

* deletion rate,
* TTL behavior,
* merge activity,
* bottom segment count,
* backup copies,
* WAL files.

## Deleted Records Still Appear In Low-Level Scans

Deletion markers are removed during compaction. Some low-level inspection paths can include deleted records by design.

Use normal read APIs for live-record semantics.

## Recovery Takes Longer Than Expected

Check:

* WAL size,
* number of unmerged in-memory segments,
* disk speed,
* compression,
* single-segment garbage collection behavior.
