![img](https://raw.githubusercontent.com/koculu/ZoneTree/main/docs/images/logo-dark.png)

# ZoneTree
ZoneTree is a persistent, high-performance, transactional, and ACID-compliant key-value database for .NET.
It can operate in memory or on disk. (Optimized for SSDs)

[![Download](https://img.shields.io/badge/download-ZoneTree-blue)](https://www.nuget.org/packages/ZoneTree/)

ZoneTree is a lightweight, transactional and high-performance LSM Tree for .NET.

It is four times faster than Facebook's RocksDB.

## Why ZoneTree?
1. It is pure C#.
2. It is fast. See benchmark below.
3. Your data is protected against crashes / power cuts.
4. Supports transactional and non-transactional access with blazing speeds and ACID guarantees.
5. You can embed your database into your assembly. Therefore, you don't have to pay the cost of maintaining/shipping another database product along with yours.

## How fast is it?

| Insert Benchmarks                               | 1M      | 2M       | 3M         | 10M        |
| ------------------------------------------------|---------|----------|------------|------------|
| int-int ZoneTree lazy WAL                       | 343 ms  | 506 ms   | 624 ms     | 2328 ms    |
| int-int ZoneTree compressed-immediate WAL       | 885 ms  | 1821 ms  | 2737 ms    | 9250 ms    |
| int-int ZoneTree immediate WAL                  | 2791 ms | 5552 ms  | 8269 ms    | 27883 ms   |
||
| str-str ZoneTree lazy WAL                       | 796 ms  | 1555 ms  | 2308 ms    | 8712 ms    |
| str-str ZoneTree compressed-immediate WAL       | 1594 ms | 3187 ms  | 4866 ms    | 17451 ms   |
| str-str ZoneTree immediate WAL                  | 3617 ms | 7083 ms  | 10481 ms   | 36714 ms   |
||
| RocksDb immediate WAL                           | NOT SUPPORTED                                |
| int-int RocksDb compressed-immediate WAL        | 8059 ms | 16188 ms | 23599 ms   | 61947 ms   |
| str-str RocksDb compressed-immediate WAL        | 8215 ms | 16146 ms | 23760 ms   | 72491 ms   |
||

Benchmark Configuration:
```c#
DiskCompressionBlockSize = 1024 * 1024 * 1;
WALCompressionBlockSize = 1024 * 1024;
DiskSegmentMode = DiskSegmentMode.MultipleDiskSegments;
```

Additional Notes:
According to our tests, ZoneTree is stable and fast even with big data.
Tested up to 200M records in desktop computers till now.

### ZoneTree offers 3 WAL modes to let you make a flexible tradeoff.

* The Immediate mode provides maximum durability but slower write speed.
 In case of a crash/power cut, the immediate mode ensures that the inserted data is not lost. RocksDb does not have immediate WAL mode. It has a WAL mode similar to the CompressedImmediate mode.
(reference:http://rocksdb.org/blog/2017/08/25/flushwal.html)

* The CompressedImmediate mode provides faster write speed but less durability.
  Compression requires chunks to be filled before appending them into the WAL file.
  It is possible to enable a periodic job to persist decompressed tail records into a separate location in a specified interval.
  See IWriteAheadLogProvider options for more details.


* The Lazy mode provides faster write speed but less durability.
  Log entries are queued to be written in a separate thread.
  Lazy mode uses compression in WAL files and provides immediate tail record persistence.

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
  maintainer.CompleteRunningTasks();
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
You can micro-manage the tree size with ZoneTree.
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

## Features
| ZoneTree Features                                          |
| ---------------------------------------------------------- |
| Works with .NET primitives, structs and classes.           |
| High Speed and Low Memory consumption.                     |
| Crash Resilience                                           |
| Optimum disk space utilization.                            |
| WAL and DiskSegment data compression.                      |
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
| MultipleDiskSegments Mode to enable dividing data files into configurable sized chunks.|

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
