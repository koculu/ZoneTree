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

## Use The Fast Path

Use `Upsert` for simple writes. Avoid transactions or atomic methods unless the correctness rule requires them.

```csharp
zoneTree.Upsert(key, value);
```

## Mutable Segment Size

Larger mutable segments can improve write batching and reduce merge frequency, but they use more memory.

Large values should use a lower `MutableSegmentMaxItemCount`.

## WAL Mode

If data can be rebuilt, `No WAL` or async WAL modes can deliver higher throughput.

If data cannot be rebuilt, choose stronger WAL durability and tune around it.

## Maintenance Throughput

If read-only in-memory segments accumulate, maintenance is not keeping up. Consider:

* lowering mutable segment size,
* increasing maintenance activity,
* reducing compression cost,
* checking storage bandwidth,
* reducing value size.

## Disk Segment Shape

For very large write-heavy databases, multipart disk segments help keep physical segment files manageable. Tune disk part sizes only after observing merge duration, file count, backup behavior, and read latency.

See [disk segment tuning](disk-segments.md).
