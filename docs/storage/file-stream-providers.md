# File Stream Providers

ZoneTree stores its physical files through `IFileStreamProvider`.

The default provider is `LocalFileStreamProvider`, which stores WAL files, metadata files, and disk segment files on the local file system. Applications can provide a different implementation when opening a factory.

```csharp
using ZoneTree;
using ZoneTree.AbstractFileStream;
using ZoneTree.Options;

var fileStreamProvider = new InMemoryFileStreamProvider();

using var zoneTree = new ZoneTreeFactory<int, string>(fileStreamProvider)
    .SetDataDirectory("data/session")
    .ConfigureWriteAheadLogOptions(options =>
    {
        options.WriteAheadLogMode = WriteAheadLogMode.None;
    })
    .OpenOrCreate();
```

## What It Controls

The file stream provider is the low-level file abstraction used by the default ZoneTree storage path.

It is used by:

* write-ahead logs,
* metadata files,
* random-access disk segment files,
* temporary replacement files created during durable writes and WAL compaction.

If you pass a provider to `ZoneTreeFactory<TKey, TValue>`, the default `WriteAheadLogProvider` and `RandomAccessDeviceManager` use that provider. Advanced applications can still replace those higher-level providers with `SetWriteAheadLogProvider` or `SetRandomAccessDeviceManager`.

## Local File Provider

`LocalFileStreamProvider` is the default. Use it for persistent databases, crash recovery, live backup, and normal production storage.

It delegates to the local file system and is the right choice when data must survive process exit or machine restart.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .OpenOrCreate();
```

## In-Memory File Provider

`InMemoryFileStreamProvider` runs ZoneTree's normal stream-based WAL, metadata, segment, and merge flow entirely in process memory.

It is useful when you want ZoneTree's ordered storage engine behavior but do not need persistence.

Good use cases:

* fast tests that should not touch the local file system,
* high-RAM machines running durability-optional workloads,
* temporary indexing, sorting, staging, or deduplication,
* data transfer and transformation pipelines where source data can be replayed,
* cache-like or rebuildable datasets.

It is not a durable provider. All data is lost when the provider instance is lost, the process exits, or the machine stops.

```csharp
using ZoneTree;
using ZoneTree.AbstractFileStream;
using ZoneTree.Options;

var provider = new InMemoryFileStreamProvider();

using var zoneTree = new ZoneTreeFactory<long, string>(provider)
    .SetDataDirectory("transfer-job")
    .ConfigureWriteAheadLogOptions(options =>
    {
        options.WriteAheadLogMode = WriteAheadLogMode.None;
    })
    .OpenOrCreate();
```

The provider stores large files in chunks so stream-based WAL and disk segment operations are not limited by the maximum size of a single `byte[]`. Small files use small chunks to avoid large allocations. `ReadAllBytes` remains a small-file convenience API and can throw when a file is too large to fit in one array.

## WAL Choice

For normal in-memory-provider workloads, prefer `WriteAheadLogMode.None`.

With `InMemoryFileStreamProvider`, WAL files are also stored in process memory. They do not protect against process termination, machine restart, power loss, or losing the provider instance. In that setup, WAL usually adds memory use and serialization work without adding durability.

Using a WAL with `InMemoryFileStreamProvider` is still useful for tests that specifically exercise WAL behavior, replay logic, WAL compaction, or recovery code while the provider instance is alive.

Use a durable provider, such as `LocalFileStreamProvider`, when WAL durability matters.

## Semantics

`InMemoryFileStreamProvider` is optimized for ZoneTree's own stream usage, not for exact file-system emulation.

Important behavior:

* writes, truncates, and replacements are visible immediately,
* `Replace` uses fast in-memory object handoff rather than copying large buffers,
* `FileShare` is intentionally ignored,
* open streams can keep referencing an old in-memory file object after delete or replace,
* per-stream `Position` is not thread-safe,
* data is scoped to the provider instance,
* WAL files written through this provider are not durable.

Use `InMemoryFileStreamProvider` only when those boundaries are acceptable.

## Custom Providers

Custom `IFileStreamProvider` implementations can redirect ZoneTree files into another storage environment.

When implementing a provider, focus first on the operations ZoneTree uses heavily:

* sequential WAL append and read,
* random-access segment reads,
* `SetLength`,
* `Flush(bool)`,
* `Replace`,
* directory creation and recursive deletion,
* small metadata reads through `ReadAllBytes` and `ReadAllText`.

The default storage pipeline assumes stream operations are reliable and that replacement is atomic or at least consistent enough for the provider's durability model. If the provider is not durable, document that clearly and use it only for rebuildable workloads.
