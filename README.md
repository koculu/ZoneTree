# ZoneTree
ZoneTree is a persistent, high-performance, transactional, and ACID key-value database for .NET.
It can operate in memory or on disk. (Optimized for SSDs)

[![Download](https://img.shields.io/badge/download-ZoneTree-blue)](https://www.nuget.org/packages/ZoneTree/)

ZoneTree is a lightweight, transactional and high-performance LSM Tree for .NET. 

LSM Tree (Log-structured merge-tree) is the most popular data structure and it is being used by many popular databases internally.

## Why ZoneTree?
1. It is pure C#. Easy to maintain, easy to develop new features.
2. It is faster than using C/C++ based key-value stores like RocksDB. Because ZoneTree does not need to transfer bytes to the native external libraries (Zero Marshaling).
3. .NET EcoSystem does not have any feature-complete and thread-safe LSM Tree that operates both in memory and on disk.
4. Supports transactional and non-transactional access with blazing speeds and ACID guarantees.
5. Why do you need an SQL database or another product for the projects that a persistent tree bundled with your code is sufficient? You don't need to maintain another product shipped with yours!
6. You decide where to put your data. You can adjust a few parameters to improve performance with more data loaded into memory whenever needed.When you don't need you can drop in memory data without a danger of a data loss.

## How fast is it?

2 Million int key and int value inserts in 7 seconds. (Config: 1M mutable segment size, 2M readonly segments merge-threshold)

20 Million int key and int value inserts in 73 seconds. (Config: 1M mutable segment size, 2M readonly segments merge-threshold)

20 Million int key and int value reads in 16 seconds. (Config: 1M mutable segment size, 2M readonly segments merge-threshold)

Doing database benchmark is tough. A proper and fair performance analysis requires a lot of work. 

For now, we are confident that ZoneTree is fast enough to be used in production.

## How to use ZoneTree?

The following sample demonstrates creating a database.
```c#
  var dataPath = "data/mydatabase";
  var walPath = "data/mydatabase/wal";
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
    .SetWriteAheadLogDirectory(walPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();
    
    // atomic (thread-safe) on single mutable-segment.
    zoneTree.Upsert(39, "Hello Zone Tree!");
    
    // atomic across all segments
    zoneTree.TryAtomicAddOrUpdate(39, "a", (x) => x + "b");
```
## How to maintain LSM Tree?
Big LSM Trees require maintenance tasks. ZoneTree provides the IZoneTreeMaintenance interface to give you full power on maintenance tasks.
It also comes with a default maintainer to let you focus on your business logic without wasting time with LSM details.
You can start using the default maintainer like in the following sample code.
Note: For small data you don't need a maintainer.
```c#
  var dataPath = "data/mydatabase";
  var walPath = "data/mydatabase/wal";

  // 1. Create your ZoneTree
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
    .SetWriteAheadLogDirectory(walPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();
 
  using var maintainer = new BasicZoneTreeMaintainer<int, string>(zoneTree);

  // 2. Read/Write data
  zoneTree.Upsert(39, "Hello ZoneTree!");

  // 3. Complete maintainer running tasks.
  maintainer.CompleteRunningTasks().AsTask().Wait();
```

## How to delete keys?
In LSM trees, the deletions are handled by upserting key/value with deleted flag.
Later on, during the compaction stage, the actual deletion happens.
ZoneTree does not implement this flag format by default. It lets the user to define the suitable deletion flag themselves.
For example, the deletion flag might be defined by user as -1 for int values.
If user wants to use any int value as a valid record, then the value-type should be changed.
For example, one can define the following struct and use this type as a value-type.
```c#
[StructLayout(LayoutKind.Sequential)]
struct MyDeletableValueType {
   int Number; 
   bool IsDeleted; 
}
```
ZoneTree gives flexibility to micro-manage the the tree size.
The following sample shows how to configure the deletion markers for your database.
```c#
using var zoneTree = new ZoneTreeFactory<int, int>()
  // Additional stuff goes here
  .SetIsValueDeletedDelegate((in int x) => x == -1)
  .SetMarkValueDeletedDelegate((ref int x) => x = -1)
  .OpenOrCreate();  
```
or
```c#
using var zoneTree = new ZoneTreeFactory<int, MyDeletableValueType>()
  // Additional stuff goes here
  .SetIsValueDeletedDelegate((in MyDeletableValueType x) => x.IsDeleted)
  .SetMarkValueDeletedDelegate((ref MyDeletableValueType x) => x.IsDeleted = true)
  .OpenOrCreate();  
```
If you forget to provide the deletion marker delegates, you can never delete the record from your database.

## How to iterate over data?

Iteration is possible in both directions, forward and backward.
Unlike other LSM tree implementations, iteration performance is equal in both directions.
The following sample shows how to do the iteration.
```c#
 using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreate();
 using var iterator = zoneTree.CreateIterator();
 while(iterator.Next()) {
    var key = iterator.CurrentKey;
    var value = iterator.CurrentValue;
 } 
 
 using var reverseIterator = zoneTree.CreateReverseIterator();
 while(reverseIterator.Next()) {
    var key = reverseIterator.CurrentKey;
    var value = reverseIterator.CurrentValue;
 }
```

## How to iterate starting with a key (Seekable Iterator)?

ZoneTreeIterator provides Seek() method to jump into any record with in O(log(n)) complexity.
That is useful for doing prefix search with forward-iterator or with backward-iterator.
```c#
 using var zoneTree = new ZoneTreeFactory<string, int>()
    // Additional stuff goes here
    .OpenOrCreate();
 using var iterator = zoneTree.CreateIterator();
 // iterator jumps into the first record starting with "SomePrefix" in O(log(n)) complexity. 
 iterator.Seek("SomePrefix");
 
 //iterator.Next() complexity is O(1)
 while(iterator.Next()) {
    var key = iterator.CurrentKey;
    var value = iterator.CurrentValue;
 } 
```


## Transaction Support
ZoneTree supports Optimistic Transaction. It is proud to announce that the ZoneTree is ACID compatible. Of course, you can use non-transactional API for the scenarios where eventual consistency is sufficient.

Please note that Transactional reads/writes are roughly three times slower than non-transactional ones.

The following sample shows how to do the transactions with ZoneTree.
```c#
 using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreateTransactional();
 try 
 {
     var txId = zoneTree.BeginTransaction();
     zoneTree.TryGet(txId, 3, out var value);
     zoneTree.Upsert(txId, 3, 9);
     zoneTree.Prepare(txId);
     var result = zoneTree.Commit(txId); 
  }
  catch(TransactionAbortedException e)
  {
      //retry or cancel
  }
```

## I need more information. Where can I find it?
I am going to write more detailed documentation as soon as possible.

## I want to contribute. What should I do?
I appreciate any contribution to the project.
These are the things I do think we need at the moment:
1. Write tests / benchmarks.
2. Write documentation.
3. Convert documentation to a website using static site generators.
4. Feature requests & bug fixes.
5. Performance improvements.
