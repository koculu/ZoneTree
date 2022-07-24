# ZoneTree
ZoneTree is a persistent, high-performance, transactional, and ACID-compliant key-value database for .NET.
It can operate in memory or on disk. (Optimized for SSDs)

[![Download](https://img.shields.io/badge/download-ZoneTree-blue)](https://www.nuget.org/packages/ZoneTree/)

ZoneTree is a lightweight, transactional and high-performance LSM Tree for .NET. 

LSM Tree (Log-structured merge-tree) is the most popular data structure and it is being used by many popular databases internally.

## Features
| ZoneTree Features                                          |
| ---------------------------------------------------------- |
| Works with .NET primitives, structs and classes.           |
| High Speed and Low Memory consumption.                     |
| Optimum disk space utilization with data compression.      |
| Very fast load/unload.                                     |
| Standard read/upsert/delete functions.                     |
| Optimistic Transaction Support                             |
| Atomic Read Modify Update                                  |
| Can work in memory.                                        |
| Can work with any disk device including cloud devices.     |
| Supports optimistic transactions.                          |
| Supports Atomicity, Consistency, Isolation, Durability.    |
| Supports Read Committed Isolation.                         |
| Immediate and Lazy modes for write ahead log.              |
| Audit support with incremental transaction log backup.     |
| Live backup.                                               |
| Configurable amount of data that can stay in memory.       |
| Partially (with sparse arrays) or completely load/unload data on disk to/from memory. |
| Forward/Backward iteration.                                |
| Allow optional dirty reads.                                |
| Embeddable.                                                |
| Optimized for SSDs.                                        |
| Exceptionless Transaction API.                             |
| Fluent Transaction API with ready to use retry capabilities. |
| Easy Maintenance.                                          |
| Configurable LSM merger.                                   |
| Transparent and simple implementation that reveals your database's internals. |
| Fully open-source with unrestrictive MIT license.          |
| Transaction Log compaction. |
| Analyze / control transactions. |
| Concurrency Control with minimum overhead by novel separation of Concurrency Stamps and Data.|
| TTL support. |
| Use your custom serializer for keys and values. |
| Use your custom comparer. |

## Why ZoneTree?
1. It is pure C#.
2. It is fast. See benchmark below.
3. Your data is protected against crashes / power cuts.
4. Supports transactional and non-transactional access with blazing speeds and ACID guarantees.
5. You can embed your database into your assembly. Therefore, you don't have to pay the cost of maintaining/shipping another database product along with yours.

## How fast is it?

| Insert Benchmarks                | 1M      | 2M       | 3M         | 10M        |
| ---------------------------------|---------|----------|------------|------------|
| int-int tree immediate WAL       | 5760 ms | 10796 ms | 16006 ms   | 54768 ms   |
| int-int tree lazy WAL            | 1198 ms | 2379 ms  | 3831 ms    | 15338 ms   |
| string-string tree immediate WAL | 7872 ms | 16065 ms | 24220 ms   | 90901 ms   |
| string-string tree lazy WAL      | 2556 ms | 5240 ms  | 7934 ms    | 29815 ms   |
| RocksDb string-string            | 8215 ms | 16146 ms | 23760 ms   | 72491 ms   |

Notes:
The bottleneck is the disk flushes on the write-ahead log. ZoneTree offers 2 WAL modes.
The Immediate mode gives the best durability with slower write speeds.
The Lazy mode is faster with less durability.
In case of crashes/power cuts, the immediate mode ensures that the inserted data is not lost.
RocksDb does not have immediate WAL mode.
(reference:|http://rocksdb.org/blog/2017/08/25/flushwal.html)

### Environment:
```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-6850K CPU 3.60GHz (Skylake), 1 CPU, 12 logical and 6 physical cores
64 GB DDR4 Memory
SSD: Samsung SSD 850 EVO 1TB
Config: 1M mutable segment size, 2M readonly segments merge-threshold
```

## How to use ZoneTree?

The following sample demonstrates creating a database.
```c#
  var dataPath = "data/mydatabase";
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
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

  // 1. Create your ZoneTree
  using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
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
ZoneTree gives flexibility to micro-manage the tree size.
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
ZoneTree supports Optimistic Transactions. It is proud to announce that the ZoneTree is ACID-compliant. Of course, you can use non-transactional API for the scenarios where eventual consistency is sufficient.

ZoneTree supports 3 way of doing transactions.
1. Fluent Transactions with ready to use retry capability.
2. Classical Transaction API.
3. Exceptionless Transaction API.

The following sample shows how to do the transactions with ZoneTree Fluent Transaction API.

```c#
using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreateTransactional();
using var transaction =
    zoneTree
        .BeginFluentTransaction()
        .Do((tx) => zoneTree.UpsertNoThrow(tx, 3, 9))
        .Do((tx) =>
        {
            if (zoneTree.TryGetNoThrow(tx, 3, out var value).IsAborted)
                return TransactionResult.Aborted();
            if (zoneTree.UpsertNoThrow(tx, 3, 21).IsAborted)
                return TransactionResult.Aborted();
            return TransactionResult.Success();
        })
        .SetRetryCountForPendingTransactions(100)
        .SetRetryCountForAbortedTransactions(10);
    await transaction.CommitAsync();
```

The following sample shows traditional way of doing transactions with ZoneTree.
```c#
 using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreateTransactional();
 try 
 {
     var txId = zoneTree.BeginTransaction();
     zoneTree.TryGet(txId, 3, out var value);
     zoneTree.Upsert(txId, 3, 9);
     var result = zoneTree.Prepare(txId);
     while (result.IsPendingTransactions) {
         Thread.Sleep(100);
         result = zoneTree.Prepare(txId);
     }
     zoneTree.Commit(txId);
  }
  catch(TransactionAbortedException e)
  {
      //retry or cancel
  }
```

## I need more information. Where can I find it?
I am going to write more detailed documentation as soon as possible.

## I want to contribute. What can I do?
I appreciate any contribution to the project.
These are the things I do think we need at the moment:
1. Write tests / benchmarks.
2. Write documentation.
3. Convert documentation to a website using static site generators.
4. Feature requests & bug fixes.
5. Performance improvements.
