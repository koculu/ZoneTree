# WAL Modes

ZoneTree protects recent writes with a write-ahead log according to the configured WAL mode.

The default mode is `AsyncCompressed`. It is the normal starting point for most applications: WAL protection stays enabled, records are compressed, and writes can remain very fast because WAL work is handled through a background path.

## Choosing A Mode

| Need | Consider |
| --- | --- |
| Default safe high-throughput WAL mode | Async compressed WAL |
| Synchronous compressed WAL acknowledgment | Sync compressed WAL |
| Simplest synchronous WAL path | Sync WAL |
| Intentional no-WAL boundary for cache/temp/rebuildable data | No WAL |

## Sync WAL

Sync WAL writes records directly to the log path before the write is considered complete.

Use it when you want the simplest synchronous WAL path and can accept lower throughput.

## Sync Compressed WAL

Sync compressed WAL stores log records in compressed form. It balances durability and smaller WAL files with compression overhead.

Use it when WAL size matters and you want synchronous WAL acknowledgment.

## Async Compressed WAL

Async compressed WAL is ZoneTree's default WAL mode. It is designed as a safe high-throughput default for ordinary persistent ZoneTree databases.

Writes are logged through a background path and compressed on disk. This gives most applications the right balance of speed, WAL protection, and storage efficiency.

## No WAL

No WAL gives the fastest write path but does not protect recent in-memory writes against process termination.

Use it for caches, rebuildable indexes, temporary stores, and data that can be reconstructed from another source.

## Durability Boundary

ZoneTree's WAL protects against process-level failures according to the selected mode and flush behavior. Hardware, operating system, and storage-device behavior still matter for full power-loss guarantees.

Choose `No WAL` only when the data-loss boundary is intentional. For most persistent data, start with the default async compressed WAL and move to a sync mode only when the application specifically needs synchronous WAL acknowledgment.
