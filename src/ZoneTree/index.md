[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

## What is ZoneTree?
ZoneTree is a persistent, high-performance, transactional, ACID-compliant [ordered key-value database](https://en.wikipedia.org/wiki/Ordered_Key-Value_Store) for NET. It can operate in memory or on local/cloud storage.

It is developed 100% in C# with no external dependencies. ZoneTree implements [LSM Tree](https://en.wikipedia.org/wiki/Log-structured_merge-tree) with novel optimizations on various parts.

## Why ZoneTree?
1. It is pure C#.
2. It is fast. See benchmark below.
3. Your data is protected against crashes / power cuts (optional).
4. Supports transactional and non-transactional access with blazing speeds and ACID guarantees.
5. You can embed your database into your assembly. Therefore, you don't have to pay the cost of maintaining/shipping another database product along with yours.
6. You can create scalable and non-scalable databases using ZoneTree as core database engine.

##  Write Performance
It is several times faster than Facebook's RocksDB and hundreds of times faster than SQLite. It is faster than any other alternative that I have tested so far.

You can achieve 100 Million integer key-value pair inserts in 20 seconds by disabling WAL You may get longer durations based on the write-ahead log mode. 

For example, with async-compressed WAL mode, you can insert 100M integer key-value pairs in 28 seconds.

The insert performance is faster for sorted distributions. If you insert randomly distributed keys, merge operations might slow down the entire process. This characteristic is almost the same with all LSM Tree databases.

Background merge operation that might take a bit longer is excluded from the insert duration because your inserted data is immediately queryable.

## Read Performance
Loading 100M integer key-value pair database is in 812 ms. The iteration on 100M key-value pairs takes 24 seconds. There are so many tuning options wait you to discover.

You may get more insight on performance in the [Performance](docs/ZoneTree/guide/performance.md) section.