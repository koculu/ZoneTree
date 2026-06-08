# Recovery

ZoneTree recovery rebuilds the latest in-memory state from metadata, disk segments, read-only segments, and write-ahead logs.

## Recovery Model

On startup, ZoneTree loads the persisted storage shape and replays WAL records for in-memory segments that were not yet merged into disk segments.

The recovery process preserves the newest value or deletion marker for each key according to ZoneTree's segment order and operation-index rules.

## Incomplete WAL Tails

Crash recovery can encounter an incomplete WAL tail. ZoneTree is designed to tolerate incomplete tail data where the WAL mode supports that recovery shape.

For compressed WAL files, ZoneTree validates compressed block boundaries before accepting the tail.

## Metadata

Metadata describes the storage shape: mutable segment, read-only segments, disk segment, bottom segments, and related options.

Metadata helps open the tree quickly, but data recovery must still respect WAL and segment contents.

## Single-Segment Garbage Collection

When enabled and applicable, ZoneTree can clean deleted values while loading a single mutable-segment-only database.

This is useful for small or simple stores where deleted entries would otherwise remain in the WAL until a merge occurs.

## Production Guidance

For production systems:

* choose a WAL mode deliberately,
* dispose trees during graceful shutdown,
* keep maintenance running for long-lived write-heavy workloads,
* test recovery with your actual configuration,
* keep backups if the data cannot be reconstructed.
