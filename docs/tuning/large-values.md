# Large Values

Large values change ZoneTree tuning. The default mutable segment size is designed to work well for small records, but 1 million large values can require too much memory.

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

Do not store mutable object graphs and then mutate them in place. Treat stored values as immutable snapshots.

Small record-like values are often better as `struct` or `readonly record struct`. Large or complex values should usually be immutable records, immutable classes, serialized payloads, or clone-and-upsert objects.

See [value mutability](../concepts/value-mutability.md).

## Compression

Large text values often compress well. Already-compressed values usually do not.

Test disk segment and WAL compression with representative data.

## Merge Cost

Large values increase merge IO and temporary buffer pressure. Watch maintenance duration and storage throughput.
