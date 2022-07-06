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
    
    // upsert a key-value pair.
    zoneTree.Upsert(39, "Hello Zone Tree!");
    // atomically update a record in database. (thread-safe)
    zoneTree.TryAddOrUpdateAtomic(39, "a", (x) => x + "b");
```
### How to maintain LSM Tree?
LSM Trees require maintenance tasks. ZoneTree provides the IZoneTreeMaintenance interface to give you full power on maintenance tasks.
It also comes with a default maintainer to let you focus on your business logic without wasting time with LSM details.
You can start using the default maintainer like in the following sample code.
```c#
  var dataPath = "data/mydatabase";
  var walPath = "data/mydatabase/wal";

  // 1. Create your ZoneTree
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new IntegerComparerAscending())
    .SetDataDirectory(dataPath)
    .SetWriteAheadLogDirectory(walPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new UnicodeStringSerializer())
    .OpenOrCreate();
 
  using var maintainer = new BasicZoneTreeMaintainer<int, string>(zoneTree);

  // 2. Read/Write data
  zoneTree.Upsert(39, "Hello ZoneTree!");

  // 3. Complete maintainer running tasks.
  maintainer.CompleteRunningTasks().AsTask().Wait();
```

### I need more information. Where can I find it?
I am going to write more detailed documentation as soon as possible.

### I want to contribute. What should I do?
I appreciate any contribution to the project.
These are the things I do think we need at the moment:
1. Write tests / benchmarks.
2. Write documentation.
3. Convert documentation to a website using static site generators.
4. Feature requests & bug fixes.
5. Performance improvements.
