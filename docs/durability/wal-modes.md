# WAL Modes

ZoneTree protects recent writes with a write-ahead log according to the configured WAL mode. The right mode depends on what the data means to your application.

## Choosing A Mode

| Need | Consider |
| --- | --- |
| Strongest ZoneTree WAL durability | Sync WAL |
| Strong durability with compressed WAL files | Sync compressed WAL |
| Very high write throughput with background WAL writes | Async compressed WAL |
| Rebuildable/cache data | No WAL |

## Sync WAL

Sync WAL writes records directly to the log path before the write is considered complete.

Use it for data that cannot be reconstructed and where write safety matters more than maximum throughput.

## Sync Compressed WAL

Sync compressed WAL stores log records in compressed form. It balances durability and smaller WAL files with compression overhead.

Use it when WAL size matters and you still want synchronous WAL behavior.

## Async Compressed WAL

Async compressed WAL is designed for very high write throughput. Writes are logged through a background path.

Use it when recent data can be replayed, reconstructed, or tolerated according to the application design.

## No WAL

No WAL gives the fastest write path but does not protect recent in-memory writes against process termination.

Use it for caches, rebuildable indexes, temporary stores, and data that can be reconstructed from another source.

## Durability Boundary

ZoneTree's WAL protects against process-level failures according to the selected mode and flush behavior. Hardware, operating system, and storage-device behavior still matter for full power-loss guarantees.

Choose the WAL mode intentionally. Do not treat it as a performance toggle only.
