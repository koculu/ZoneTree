# Serializers And Comparers

Serializers and comparers define the physical meaning of a ZoneTree keyspace.

The comparer defines key order. The serializers define how keys and values become bytes in WALs, disk segments, backup record batches, and restored data.

## Known Types

`ZoneTreeFactory<TKey, TValue>` fills default serializers and comparers for supported common types when they are not configured explicitly.

Built-in defaults cover primitive numeric types, `bool`, `char`, `DateTime`, `decimal`, `double`, `Guid`, `string`, and `Memory<byte>` where applicable.

For byte-array-like keys and values, use `Memory<byte>`. `byte[]` is intentionally rejected by the known-type helper because mutable arrays are a poor key shape and do not provide the same value semantics.

## Serializers

Serializers convert keys and values to bytes. They affect:

* WAL size,
* disk segment size,
* backup record batch size,
* CPU usage,
* compression behavior,
* compatibility across application versions,
* restore and recovery behavior.

Use built-in serializers for common primitive types. Use custom serializers when you need a compact binary layout, a stable external format, or a domain-specific representation.

```csharp
using ZoneTree.Serializers;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();
```

Serializer output size directly affects merge cost, WAL cost, disk size, and compression ratio.

## Comparers

Comparers define key order. They affect:

* lookup,
* iteration,
* range scans,
* sparse indexes,
* disk segment layout,
* merge behavior,
* partition and index design.

```csharp
using ZoneTree.Comparers;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetComparer(new Int32ComparerAscending())
    .OpenOrCreate();
```

For string keys, choose the comparer intentionally. Ordinal, culture-aware, case-sensitive, and case-insensitive ordering all create different keyspaces.

For structured keys, implement an `IRefComparer<TKey>` that orders fields in the same order your range scans need.

## Persisted Identity

ZoneTree stores type identity in metadata:

* key type,
* value type,
* comparer type,
* key serializer type,
* value serializer type.

When opening an existing database, ZoneTree validates those identities against the factory configuration.

The metadata check protects the database from being opened with the wrong components. The ordering and binary format themselves are still part of your storage contract. If a custom comparer or serializer keeps the same .NET type but changes behavior, treat that as a storage migration.

## Changing Order Or Format

To change key ordering, key encoding, or value encoding for an existing database, create a new ZoneTree and copy or rebuild the data.

Good migration shapes:

* snapshot iterator from old tree into new tree,
* live rebuild plus application operation stream,
* backup/restore into a new layout when the backup format is compatible with the new target,
* application-level export/import.

The important rule is simple: persisted keys must remain ordered according to the comparer used to read them.

## Fixed-Size Layouts

Small unmanaged structs can unlock simpler disk segment layouts because ZoneTree can use fixed-size key and/or value segment variations.

Good fits include:

* counters,
* scores,
* offsets,
* timestamps,
* queue pointers,
* compact index values.

```csharp
public readonly record struct UserScore(int Score, long UpdatedAt);
```

Reference types and variable-length values are also valid. They are the right shape for many products. Just account for their storage consequences: serializer cost, WAL size, disk size, compression behavior, and mutable-segment memory.

## Deletion Defaults

ZoneTree uses deletion delegates to decide whether a value represents a deletion marker and how to mark a value deleted.

For known value types, the default deletion marker is usually the default value. For reference-containing value types, the default deletion check treats `null` as deleted.

If the default value is a real value in your model, configure deletion explicitly or disable deletion:

```csharp
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetDataDirectory("data/app")
    .DisableDeletion()
    .OpenOrCreate();
```

See [deletion markers and TTL](../concepts/deletion-markers-and-ttl.md).

## Versioning

For long-lived databases, custom serializers should be explicit and version-aware.

Common approaches:

* keep the binary format stable,
* include a version byte or header in the value payload,
* migrate into a new ZoneTree when the format changes,
* use separate key ranges or partitions for major format boundaries.

Stored bytes can outlive the application build that wrote them. Serializer design is part of the database design.

## Practical Model

Think about serializers and comparers this way:

* comparer = the order of the world,
* key serializer = how that ordered key is persisted,
* value serializer = how the payload is persisted,
* metadata = the identity check that keeps the opened tree aligned with its storage shape.

Choose these pieces early, document them for the application, and treat changes as migrations.
