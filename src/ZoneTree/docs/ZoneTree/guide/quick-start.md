## Quick Start
Add ZoneTree nuget package reference to your project.
```shell
dotnet add package ZoneTree 
```

## How to use ZoneTree?
The following sample shows the most basic setup of a ZoneTree database.

```C#
using var zoneTree = new ZoneTreeFactory<int, string>()
   .OpenOrCreate();
zoneTree.Upsert(39, "Hello Zone Tree");
```

The following samples demonstrate creating a ZoneTree database and inserting some key-value pairs.
The key comparer and serializers are selected automatically in the first example.
They can also be configured manually through ZoneTreeFactory as follows.

```C#
var dataPath = "data/mydatabase";
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();
    
zoneTree.Upsert(39, "Hello Zone Tree");

zoneTree
   .TryAtomicAddOrUpdate(39, 
      "Hello", void (ref string x) => 
      {
         x += "!";
         return true;
      });

if (zoneTree.TryGet(55, out var value))
    Debug.Assert(value == "Hello Zone Tree!");
```

## How to maintain LSM Tree?
Big LSM Trees require maintenance tasks. ZoneTree provides the IZoneTreeMaintenance interface to give you full power on maintenance tasks.
It also comes with a default maintainer to let you focus on your business logic without wasting time with LSM details.
You can start using the default maintainer like in the following sample code.
Note: For small data you don't need a maintainer.
Following sample demonstraces creating a ZoneTree database with basic maintainer and inserting 10M key-value pairs.
```C#
var dataPath = "data/mydatabase";
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetDataDirectory(dataPath)
    .OpenOrCreate();
    
using var maintainer = new BasicZoneTreeMaintainer<int, string>(zoneTree);
for (var i = 0 ; i < 10_000_000; ++i){
   zoneTree.Insert(i, i+i);
}
 
// Ensure maintainer merge operations are completed before tree disposal.
maintainer.CompleteRunningTasks();
```

## How to merge data to the disk segment manually?
Create Zone Tree insert 10M key-value pairs and manually merge in the end.
```C#
var dataPath = "data/mydatabase";
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetDataDirectory(dataPath)
    .OpenOrCreate();
    
for (var i = 0 ; i < 10_000_000; ++i){
   zoneTree.Insert(i, i+i);
}

zoneTree.Maintenance.MoveSegmentZeroForward();
zoneTree.Maintenance.StartMergeOperation()?.Join();
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

You can use built in generic [Deletable&lt;TValue&gt;](/docs/ZoneTree/api/Tenray.ZoneTree.Core.Deletable-1.html) for deletion.

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