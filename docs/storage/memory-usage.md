# Memory Usage

ZoneTree is designed for datasets larger than memory. It does not need to load the whole database into RAM.

## Where Memory Goes

Memory usage mainly comes from:

* active mutable segment,
* frozen in-memory segments waiting for merge,
* disk block cache,
* sparse indexes,
* iterators that pin active segments,
* merge buffers,
* WAL processing buffers,
* user key/value objects.

Small immutable structs can reduce GC pressure for in-memory values. Mutable reference types can create correctness problems if they are mutated after insertion, so treat stored values as immutable snapshots.

See [value mutability](../concepts/value-mutability.md).

## Mutable Segment Size

The most important write-side memory control is `MutableSegmentMaxItemCount`.

By default, ZoneTree starts moving a mutable segment toward disk after `1_000_000` records. This is a good default for small keys and values. For large strings, documents, or payload objects, lower the limit because record count and byte size are very different things.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetMutableSegmentMaxItemCount(10_000)
    .OpenOrCreate();
```

## .NET GC Behavior

ZoneTree is built as a native .NET storage engine and uses the .NET garbage collector as part of its design. The GC is highly optimized for modern application workloads and helps keep ZoneTree simple, safe, and fast without requiring manual memory management.

When observing memory usage, remember that .NET may keep freed memory available for reuse instead of returning it to the operating system immediately. Process memory can remain high even after segments are merged or released.

Use .NET memory diagnostics when you need to measure live ZoneTree data precisely.

## Read-Heavy Workloads

For read-heavy workloads, memory is mostly shaped by cache behavior and the active working set.

## Write-Heavy Workloads

For write-heavy workloads, memory is mostly shaped by mutable segment size and how quickly maintenance can move frozen segments to disk.
