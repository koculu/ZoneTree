# Time-Series Storage

ZoneTree works well for time-series layouts because ordered keys make time ranges cheap to scan.

## Key Layout

Common layout:

```text
seriesId:timestamp -> value
```

For multi-tenant systems:

```text
tenantId:seriesId:timestamp -> value
```

## Latest First

Use reverse iteration for latest-first reads. You can also encode timestamps in descending order if that better fits your query model.

```csharp
var prefix = "sensor-7:";
var upperBound = "sensor-7:2026-06-08T23:59:59.9999999Z";

using var iterator = readings.CreateReverseIterator();
iterator.Seek(upperBound);

while (iterator.Next())
{
    if (!iterator.CurrentKey.StartsWith(prefix, StringComparison.Ordinal))
        break;

    Console.WriteLine(iterator.CurrentValue);
}
```

Use sortable timestamp encodings such as fixed-width ticks or round-trip UTC timestamps.

## Retention

Retention can be modeled with:

* TTL-style values,
* range deletes at the application layer,
* partitioned trees by time window,
* rebuild/compaction jobs.

## Large Series

For very large series, consider partitioning by tenant, time bucket, or series group. Multiple ZoneTrees can keep operational boundaries smaller.

## Aggregates

Aggregates can be maintained in separate key ranges or separate trees. Use atomic operations for single aggregate counters and transactions when several aggregate keys must update together.
