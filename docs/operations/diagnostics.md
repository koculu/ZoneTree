# Diagnostics

ZoneTree exposes enough operational shape to build diagnostics around memory, maintenance, and storage behavior.

## What To Observe

Track:

* mutable segment record count,
* read-only segment count,
* disk segment size,
* bottom segment count,
* merge start/end,
* WAL size,
* backup duration,
* iterator lifetime,
* process memory and live managed memory.

## Maintenance Signals

Maintenance events can be used to observe segment lifecycle. They are useful for logs, metrics, and operational dashboards.

## Logs

Configure logging according to your application needs.

For production systems, capture:

* failed drops,
* failed merges,
* recovery errors,
* WAL corruption or incomplete-tail events,
* unusually long maintenance tasks.

## Memory Diagnostics

Use .NET tools when diagnosing memory:

* live object counts,
* LOH usage,
* allocation rate,
* GC pauses,
* retained objects,
* pinned objects.

OS process memory alone is not enough to prove live ZoneTree memory usage.

## Benchmark Diagnostics

When benchmarking, record:

* key/value type,
* serializers,
* comparer,
* WAL mode,
* compression settings,
* mutable segment size,
* disk segment mode,
* storage hardware,
* maintenance settings.
