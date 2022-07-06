# ZoneTree
ZoneTree is a persistent, high-performance key-value database for .NET.
It can operate in memory or on disk. (Optimized for SSDs)

ZoneTree is a fast and high-performance LSM Tree for .NET. 

LSM Tree (Log-structured merge-tree) is the most popular data structure and it is being used by many popular databases internally.

## Why ZoneTree?
1. It is pure C#. Easy to maintain, easy to develop new features.
2. It is faster than using C/C++ based key-value stores like RocksDB. Because ZoneTree does not need to transfer bytes to the native external libraries (Zero Marshaling).
3. .NET EcoSystem does not have any feature-complete and thread-safe LSM Tree that operates both in memory and on disk.

## How to use ZoneTree?

### The following sample demonstrates creating a database.
```c#
  var dataPath = "data/mydatabase";
  var walPath = "data/mydatabase/wal";
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new IntegerComparerAscending())
    .SetDataDirectory(dataPath)
    .SetWriteAheadLogDirectory(walPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new UnicodeStringSerializer())
    .OpenOrCreate();
    zoneTree.Upsert(39, "Hello Zone Tree!");
```
