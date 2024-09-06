[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

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

if (zoneTree.TryGet(39, out var value))
    Debug.Assert(value == "Hello Zone Tree!");
```

## How to maintain LSM Tree?
Big LSM Trees require maintenance tasks. ZoneTree provides the IZoneTreeMaintenance interface to give you full power on maintenance tasks.
It also comes with a default maintainer to let you focus on your business logic without wasting time with LSM details.
You can start using the default maintainer like in the following sample code.
Note: For small data you don't need a maintainer.
Following sample demonstraces creating a ZoneTree database, creating the maintainer and inserting 10M key-value pairs.
```C#
var dataPath = "data/mydatabase";
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetDataDirectory(dataPath)
    .OpenOrCreate();
    
using var maintainer = zoneTree.CreateMaintainer();
maintainer.EnableJobForCleaningInactiveCaches = true;
for (var i = 0; i < 10_000_000; ++i){
   zoneTree.Insert(i, i+i);
}
 
// Ensure maintainer merge operations are completed before tree disposal.
maintainer.WaitForBackgroundThreads();
```

## How to merge data to the disk segment manually?
Create Zone Tree insert 10M key-value pairs and manually merge in the end.
```C#
var dataPath = "data/mydatabase";
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetDataDirectory(dataPath)
    .OpenOrCreate();
    
for (var i = 0; i < 10_000_000; ++i){
   zoneTree.Insert(i, i+i);
}

zoneTree.Maintenance.MoveMutableSegmentForward();
zoneTree.Maintenance.StartMergeOperation()?.Join();
```

## How to delete keys?
In Log-Structured Merge (LSM) trees, deletions are managed by upserting a key/value pair with a deletion marker. The actual removal of the data occurs during the compaction stage. In ZoneTree, by default, the system assumes that the default values indicate deletion. However, you can customize this behavior by defining a specific deletion flag, such as using -1 for integer values or completely disable deletion by calling DisableDeletion method.

### Custom Deletion Flag
If you need more control over how deletions are handled, you can define a custom structure to represent your values and their deletion status. For example:

```c#
[StructLayout(LayoutKind.Sequential)]
struct MyDeletableValueType {
   int Number; 
   bool IsDeleted; 
}
```

This struct allows you to include a boolean flag indicating whether a value is deleted. You can then use this custom type as the value type in your ZoneTree.

### Configuring Deletion Markers
ZoneTree provides flexibility in managing the tree size by allowing you to configure how deletion markers are set and identified. Below are examples of how you can configure these markers for your database:

#### Example 1: Using an Integer Deletion Flag
In this example, -1 is used as the deletion marker for integer values:

```c#
using var zoneTree = new ZoneTreeFactory<int, int>()
  // Additional stuff goes here
  .SetIsDeletedDelegate((in int x) => x == -1)
  .SetMarkValueDeletedDelegate((ref int x) => x = -1)
  .OpenOrCreate();  
```

#### Example 2: Using a Custom Struct for Deletion
Alternatively, if you're using a custom struct to manage deletions, you can configure ZoneTree to recognize and mark deletions as follows:

```c#
using var zoneTree = new ZoneTreeFactory<int, MyDeletableValueType>()
  // Additional stuff goes here
  .SetIsDeletedDelegate((in MyDeletableValueType x) => x.IsDeleted)
  .SetMarkValueDeletedDelegate((ref MyDeletableValueType x) => x.IsDeleted = true)
  .OpenOrCreate();  
```

You can also use built in generic [Deletable&lt;TValue&gt;](/docs/ZoneTree/api/Tenray.ZoneTree.PresetTypes.Deletable-1.html) for deletion.

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