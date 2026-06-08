# Large Values

Large values change ZoneTree tuning. The default mutable segment size is designed to work well for small records, but `1_000_000` large values can require too much memory.

## Lower Mutable Segment Count

For large strings, documents, JSON blobs, or payload objects, lower `MutableSegmentMaxItemCount`.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/large-values")
    .SetMutableSegmentMaxItemCount(10_000)
    .OpenOrCreate();
```

Pick a limit based on expected value size and available memory.

## Consider Value Shape

If values are very large, consider whether the tree should store:

* the full value,
* a compact serialized form,
* a pointer to external content,
* metadata plus a content address.

ZoneTree is an engine; your product can choose the storage model that fits.

## Mutable Reference Types

Treat values returned by normal reads as immutable snapshots. Do not mutate a returned object and assume that creates a durable ZoneTree write.

Small record-like values are often better as `struct` or `readonly record struct`. Large or complex values should usually be immutable records, immutable classes, serialized payloads, or clone-and-upsert objects.

See [value mutability](../concepts/value-mutability.md).

## Compression

Large text values often compress well. Already-compressed values usually do not.

Test disk segment and WAL compression with representative data.

## Merge Cost

Large values increase merge IO and temporary buffer pressure. Watch maintenance duration and storage throughput.

## Symptom Guide

| Symptom | Likely pressure | First actions |
| --- | --- | --- |
| Memory grows quickly during inserts | too many large values in the mutable segment | lower `MutableSegmentMaxItemCount` |
| Merges are slow | large payloads are being repeatedly rewritten | reduce value size, store pointers/content addresses, tune compression |
| WAL files are large | values are large or poorly compressible | test compressed WAL modes with real payloads |
| Random reads are expensive | large compressed blocks or large serialized values | tune compression block size; consider splitting metadata from payload |
