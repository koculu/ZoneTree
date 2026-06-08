# Serializers And Comparers

Serializers and comparers define the physical meaning of your keyspace.

## Serializers

Serializers convert keys and values to bytes. They affect:

* storage format,
* WAL size,
* disk segment size,
* CPU usage,
* compatibility across versions.

Use built-in serializers for common primitive types. Use custom serializers when you need a compact or stable binary layout.

Serializer output size directly affects disk layout, WAL size, compression behavior, and merge cost.

## Comparers

Comparers define key order. They affect:

* lookup,
* iteration,
* range scans,
* disk segment layout,
* merge behavior.

Choose comparers before creating a database. Changing comparer semantics for an existing database changes the meaning of its stored order.

## Example

```csharp
using ZoneTree;
using ZoneTree.Comparers;
using ZoneTree.Serializers;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .SetDataDirectory("data/app")
    .OpenOrCreate();
```

## Versioning

If you write custom serializers, plan for versioning. Stored bytes may outlive the application build that wrote them.

For long-lived databases, prefer explicit binary layouts over reflection-heavy or version-fragile formats.

## Fixed-Size Value Shapes

Small unmanaged structs can reduce GC pressure and let ZoneTree use fixed-size disk segment layouts. This can be a strong fit for counters, scores, offsets, timestamps, queue pointers, and compact index values.

Use reference types or variable-length payloads when the data model needs them, but keep the physical storage consequences in mind.
