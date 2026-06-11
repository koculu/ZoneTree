# Write-Heavy Workloads

Write-heavy workloads stress the mutable segment, WAL, and maintenance pipeline.

## Main Controls

Tune:

* WAL mode,
* mutable segment max item count,
* maintenance behavior,
* disk segment size,
* compression,
* serializers.

## Symptom Guide

| Symptom | Likely pressure | First actions |
| --- | --- | --- |
| Process memory grows during heavy writes | mutable or read-only in-memory segments are too large | lower `MutableSegmentMaxItemCount`; keep a maintainer alive; check value size |
| Many read-only segments accumulate | maintenance is not keeping up | start/keep `CreateMaintainer`; lower mutable segment size; inspect merge duration |
| Writes are slower than expected | WAL mode, serializer cost, compression, storage latency | use `Upsert`; avoid unnecessary atomic/transaction APIs; benchmark WAL modes |
| Merge takes too long | disk IO, compression, large values, large segment size | reduce value size; tune compression; review disk segment sizing |
| WAL files grow | mutable segments are not being merged/compacted, or transaction logs retain history | keep maintenance healthy; review transaction cleanup and backup settings |

## Use The Fast Path

Use `Upsert` for simple writes. Avoid transactions or atomic methods unless the correctness rule requires them.

```csharp
zoneTree.Upsert(key, value);
```

## Mutable Segment Size

Larger mutable segments can improve write batching and reduce merge frequency, but they use more memory.

The default mutable segment limit is `1_000_000` records. That is a good general-purpose default for compact values. For large strings, JSON documents, or object payloads, tune it by expected byte size instead of treating `1_000_000` records as a memory budget.

Large values should use a lower `MutableSegmentMaxItemCount`.

## WAL Mode

The default async compressed WAL is usually the right starting point for write-heavy workloads. It keeps WAL protection enabled while allowing very high throughput through background WAL writes and compression.

Use sync WAL modes only when the application needs synchronous WAL acknowledgment. Use `No WAL` only for cache, temporary, or intentionally rebuildable data.

## Maintenance Throughput

If read-only in-memory segments accumulate, maintenance is not keeping up. Consider:

* lowering mutable segment size,
* increasing maintenance activity,
* reducing compression cost,
* checking storage bandwidth,
* reducing value size.

```csharp
using var maintainer = zoneTree.CreateMaintainer();

maintainer.MaximumReadOnlySegmentCount = 32;
maintainer.ThresholdForMergeOperationStart = 500_000;
```

## Disk Segment Shape

For very large write-heavy databases, multipart disk segments help keep physical segment files manageable. Tune disk part sizes only after observing merge duration, file count, backup behavior, and read latency.

The default disk segment max item count is `20_000_000` records. Multipart disk segments target `1_500_000` to `3_000_000` records per part by default.

See [disk segment tuning](disk-segments.md) and [write amplification](write-amplification.md).
