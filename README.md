![ZoneTree Logo](https://raw.githubusercontent.com/koculu/ZoneTree/main/src/ZoneTree/docs/ZoneTree/images/logo2.png)

# ZoneTree

ZoneTree is a **persistent**, **high-performance**, **transactional**, and **ACID-compliant** [ordered key-value database](https://en.wikipedia.org/wiki/Ordered_Key-Value_Store) for .NET. It operates seamlessly both **in-memory** and on **local/cloud storage**, making it an ideal choice for a wide range of applications requiring efficient data management.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
[![Downloads](https://img.shields.io/nuget/dt/ZoneTree)](https://www.nuget.org/packages/ZoneTree/)
![Platform](https://img.shields.io/badge/platform-.NET-blue.svg)
![Build](https://img.shields.io/badge/build-passing-brightgreen.svg)
[![Join Discord](https://dcbadge.vercel.app/api/server/d9aDtzVNNv?logoColor=f1c400&theme=discord&style=flat)](https://discord.gg/d9aDtzVNNv)

## Table of Contents

- [Why Choose ZoneTree?](#why-choose-zonetree)
- [Performance](#performance)
  - [Benchmark Results](#benchmark-results)
  - [Write-Ahead Log (WAL) Modes](#write-ahead-log-wal-modes)
  - [Benchmark Environment](#benchmark-environment)
- [Getting Started](#getting-started)
  - [Basic Usage](#basic-usage)
  - [Creating a Database](#creating-a-database)
- [Maintaining the LSM Tree](#maintaining-the-lsm-tree)
- [Handling Deletions](#handling-deletions)
  - [Using an Integer Deletion Flag](#using-an-integer-deletion-flag)
  - [Using a Custom Struct for Deletion](#using-a-custom-struct-for-deletion)
- [Data Iteration](#data-iteration)
  - [Forward and Backward Iteration](#forward-and-backward-iteration)
  - [Seekable Iterator](#seekable-iterator)
- [Transaction Support](#transaction-support)
  - [Fluent Transactions](#fluent-transactions)
  - [Classical Transactions](#classical-transactions)
  - [Exceptionless Transactions](#exceptionless-transactions)
- [Feature Highlights](#feature-highlights)
- [ZoneTree.FullTextSearch](#zonetreefulltextsearch)
- [Documentation](#documentation)
- [Contributing](#contributing)
- [License](#license)

---

## Why Choose ZoneTree?

1. **Pure C# Implementation**: ZoneTree is developed entirely in C#, ensuring seamless integration and deployment within .NET ecosystems without external dependencies.

2. **Exceptional Performance**: Demonstrates performance several times faster than Facebook's RocksDB and hundreds of times faster than SQLite. Optimized for both speed and efficiency.

3. **Data Durability and Crash Resilience**: Provides optional durability features that protect data against crashes and power outages, ensuring data integrity under all circumstances.

4. **Transactional and ACID-Compliant**: Supports both transactional and non-transactional access with full ACID guarantees, offering flexibility for various application requirements.

5. **Embeddable Database Engine**: Easily embed ZoneTree into applications, eliminating the overhead of maintaining or shipping separate database products.

6. **Scalability**: Capable of handling small to massive datasets, allowing the creation of both scalable and non-scalable databases tailored to specific needs.

---

## Performance

ZoneTree sets new standards in database performance, showcasing remarkable speeds in data insertion, loading, and iteration operations.

### Benchmark Results

- **100 Million integer key-value pairs** inserted in **20 seconds** using **WAL mode = NONE**.
- **Loading** database with 100 million integer key-value pairs takes **812 milliseconds**.
- **Iterating** over 100 million key-value pairs completes in **24 seconds**.

**Detailed Benchmark for Various Modes:**

| Insert Benchmarks                         | 1M            | 2M       | 3M       | 10M      |
| ----------------------------------------- | ------------- | -------- | -------- | -------- |
| **int-int ZoneTree async-compressed WAL** | 267 ms        | 464 ms   | 716 ms   | 2693 ms  |
| **int-int ZoneTree sync-compressed WAL**  | 834 ms        | 1617 ms  | 2546 ms  | 8642 ms  |
| **int-int ZoneTree sync WAL**             | 2742 ms       | 5533 ms  | 8242 ms  | 27497 ms |
|                                           |               |          |          |          |
| **str-str ZoneTree async-compressed WAL** | 892 ms        | 1833 ms  | 2711 ms  | 9443 ms  |
| **str-str ZoneTree sync-compressed WAL**  | 1752 ms       | 3397 ms  | 5070 ms  | 19153 ms |
| **str-str ZoneTree sync WAL**             | 3488 ms       | 7002 ms  | 10483 ms | 38727 ms |
|                                           |               |          |          |          |
| **RocksDb sync WAL (10K => 11 sec)**      | ~1,100,000 ms | N/A      | N/A      | N/A      |
| **int-int RocksDb sync-compressed WAL**   | 8059 ms       | 16188 ms | 23599 ms | 61947 ms |
| **str-str RocksDb sync-compressed WAL**   | 8215 ms       | 16146 ms | 23760 ms | 72491 ms |

**[Full Benchmark Results](https://raw.githubusercontent.com/koculu/ZoneTree/main/src/Playground/BenchmarkForAllModes.txt)**

**Benchmark Configuration:**

```csharp
DiskCompressionBlockSize = 10 * 1024 * 1024; // 10 MB
WALCompressionBlockSize = 32 * 1024 * 8;     // 256 KB
DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;
MutableSegmentMaxItemCount = 1_000_000;
ThresholdForMergeOperationStart = 2_000_000;
```

**Additional Notes:**

- ZoneTree has been tested successfully with up to **1 billion records** on standard desktop computers, demonstrating stability and efficiency even with very large datasets.

### Write-Ahead Log (WAL) Modes

ZoneTree offers **four WAL modes** to provide flexibility between performance and durability:

1. **Sync Mode**:

   - **Durability**: Maximum.
   - **Performance**: Slower write speed.
   - **Use Case**: Ensures data is not lost in case of crashes or power cuts.

2. **Sync-Compressed Mode**:

   - **Durability**: High, but slightly less than sync mode.
   - **Performance**: Faster write speed due to compression.
   - **Use Case**: Balances durability and performance; periodic jobs can persist decompressed tail records for added safety.

3. **Async-Compressed Mode**:

   - **Durability**: Moderate.
   - **Performance**: Very fast write speed; logs are written in a separate thread.
   - **Use Case**: Suitable where immediate durability is less critical but performance is paramount.

4. **None Mode**:
   - **Durability**: No immediate durability; relies on manual or automatic disk saves.
   - **Performance**: Maximum possible.
   - **Use Case**: Ideal for scenarios where performance is critical and data can be reconstructed or is not mission-critical.

### Benchmark Environment

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Processor: Intel Core i7-6850K CPU @ 3.60GHz (Skylake), 12 logical cores, 6 physical cores
Memory: 64 GB DDR4
Storage: Samsung SSD 850 EVO 1TB
Configuration: 1M mutable segment size, 2M readonly segments merge-threshold
```

---

## Getting Started

ZoneTree is designed for ease of use, allowing developers to integrate and utilize its capabilities with minimal setup.

### Basic Usage

```csharp
using Tenray.ZoneTree;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .OpenOrCreate();

zoneTree.Upsert(39, "Hello ZoneTree");
```

### Creating a Database

```csharp
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;

var dataPath = "data/mydatabase";

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();

// Atomic (thread-safe) operation on single mutable-segment.
zoneTree.Upsert(39, "Hello ZoneTree!");

// Atomic operation across all segments.
zoneTree.TryAtomicAddOrUpdate(39, "a", (ref string x) =>
{
    x += "b";
    return true;
});
```

---

## Maintaining the LSM Tree

Large-scale LSM Trees require periodic maintenance to ensure optimal performance and resource utilization. ZoneTree provides the `IZoneTreeMaintenance` interface to facilitate comprehensive maintenance tasks.

**Example Usage:**

```csharp
using Tenray.ZoneTree;
using Tenray.ZoneTree.Maintenance;

var dataPath = "data/mydatabase";

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory(dataPath)
    .SetKeySerializer(new Int32Serializer())
    .SetValueSerializer(new Utf8StringSerializer())
    .OpenOrCreate();

using var maintainer = zoneTree.CreateMaintainer();
maintainer.EnableJobForCleaningInactiveCaches = true;

// Perform read/write operations.
zoneTree.Upsert(39, "Hello ZoneTree!");

// Wait for background maintenance tasks to complete.
maintainer.WaitForBackgroundThreads();
```

**Note:** For smaller datasets, maintenance tasks may not be necessary. The default maintainer allows developers to focus on core business logic without delving into LSM tree intricacies.

---

## Handling Deletions

In **Log-Structured Merge (LSM) trees**, deletions are managed by upserting a key/value pair with a **deletion marker**. Actual data removal occurs during the **compaction** stage.

By default, ZoneTree assumes that **default values** indicate deletion. This behavior can be customized by defining specific deletion flags or disabling deletions entirely using the `DisableDeletion` method.

### Using an Integer Deletion Flag

In this example, `-1` is used as the deletion marker for integer values:

```csharp
using Tenray.ZoneTree;

using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetIsValueDeletedDelegate((in int x) => x == -1)
    .SetMarkValueDeletedDelegate((ref int x) => x = -1)
    .OpenOrCreate();

// Deleting a key by setting its value to -1
zoneTree.Upsert(42, -1);
```

### Using a Custom Struct for Deletion

For more control, define a custom structure to represent values and their deletion status:

```csharp
using System.Runtime.InteropServices;
using Tenray.ZoneTree;

[StructLayout(LayoutKind.Sequential)]
struct MyDeletableValueType
{
    public int Number;
    public bool IsDeleted;
}

using var zoneTree = new ZoneTreeFactory<int, MyDeletableValueType>()
    .SetIsValueDeletedDelegate((in MyDeletableValueType x) => x.IsDeleted)
    .SetMarkValueDeletedDelegate((ref MyDeletableValueType x) => x.IsDeleted = true)
    .OpenOrCreate();

// Deleting a key by setting the IsDeleted flag
zoneTree.Upsert(42, new MyDeletableValueType { Number = 0, IsDeleted = true });
```

---

## Data Iteration

ZoneTree provides efficient mechanisms to iterate over data both **forward** and **backward**, with equal performance in both directions. Iterators also support **seek** operations for quick access to specific keys.

### Forward and Backward Iteration

```csharp
using Tenray.ZoneTree;
using Tenray.ZoneTree.Collections;

using var zoneTree = new ZoneTreeFactory<int, int>()
    .OpenOrCreate();

// Forward iteration
using var iterator = zoneTree.CreateIterator();
while (iterator.Next())
{
    var key = iterator.CurrentKey;
    var value = iterator.CurrentValue;
    // Process key and value
}

// Backward iteration
using var reverseIterator = zoneTree.CreateReverseIterator();
while (reverseIterator.Next())
{
    var key = reverseIterator.CurrentKey;
    var value = reverseIterator.CurrentValue;
    // Process key and value
}
```

### Seekable Iterator

The `ZoneTreeIterator` supports the `Seek()` method to jump to any record with **O(log(n))** complexity, useful for prefix searches and range queries.

```csharp
using Tenray.ZoneTree;
using Tenray.ZoneTree.Collections;

using var zoneTree = new ZoneTreeFactory<string, int>()
    .OpenOrCreate();

using var iterator = zoneTree.CreateIterator();

// Jump to the first record starting with "SomePrefix"
if (iterator.Seek("SomePrefix"))
{
    do
    {
        var key = iterator.CurrentKey;
        var value = iterator.CurrentValue;
        // Process key and value
    }
    while (iterator.Next());
}
```

---

## Transaction Support

ZoneTree supports **Optimistic Transactions**, ensuring **ACID compliance** while offering flexibility through various transaction models:

1. **Fluent Transactions**: Provides an intuitive, chainable API with built-in retry capabilities.
2. **Classical Transactions**: Traditional approach with explicit control over transaction lifecycle.
3. **Exceptionless Transactions**: Allows transaction management without relying on exceptions for control flow.

### Fluent Transactions

```csharp
using System.Threading.Tasks;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Transaction;

using var zoneTree = new ZoneTreeFactory<int, int>()
    .OpenOrCreateTransactional();

using var transaction = zoneTree
    .BeginFluentTransaction()
    .Do(tx => zoneTree.UpsertNoThrow(tx, 3, 9))
    .Do(tx =>
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

### Classical Transactions

```csharp
using System;
using System.Threading;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Transaction;

using var zoneTree = new ZoneTreeFactory<int, int>()
    .OpenOrCreateTransactional();

try
{
    var txId = zoneTree.BeginTransaction();

    zoneTree.TryGet(txId, 3, out var value);
    zoneTree.Upsert(txId, 3, 9);

    var result = zoneTree.Prepare(txId);
    while (result.IsPendingTransactions)
    {
        Thread.Sleep(100);
        result = zoneTree.Prepare(txId);
    }

    zoneTree.Commit(txId);
}
catch (TransactionAbortedException)
{
    // Handle aborted transaction (retry or cancel)
}
```

### Exceptionless Transactions

```csharp
using System;
using System.Threading.Tasks;
using Tenray.ZoneTree.Transactional;

public async Task<bool> ExecuteTransactionWithRetryAsync(ZoneTreeFactory<int, int> zoneTreeFactory, int maxRetries = 3)
{
    using var zoneTree = zoneTreeFactory.OpenOrCreateTransactional();
    var transactionId = zoneTree.BeginTransaction();
    int retryCount = 0;

    while (retryCount <= maxRetries)
    {
        var result = zoneTree.UpsertNoThrow(transactionId, 1, 100);
        if (result.IsAborted)
        {
            // Abort the transaction and return false on failure.
            return false;
        }

        result = zoneTree.UpsertNoThrow(transactionId, 2, 200);
        if (result.IsAborted)
        {
            // Abort the transaction and return false on failure.
            return false;
        }

        var prepareResult = zoneTree.PrepareNoThrow(transactionId);
        if (prepareResult.IsAborted)
        {
            // Abort the transaction and return false on failure.
            return false;
        }

        if (prepareResult.IsPendingTransactions)
        {
            // Optionally wait or handle pending transactions
            await Task.Delay(100); // Simple delay before retrying
            retryCount++;
            continue; // Retry the transaction
        }

        if (prepareResult.IsReadyToCommit)
        {
            var commitResult = zoneTree.CommitNoThrow(transactionId);
            if (commitResult.IsAborted)
            {
                // Abort the transaction and return false on failure.
                return false;
            }
            // Transaction committed successfully
            return true;
        }
    }

    // Return false if retries are exhausted without a successful commit.
    return false;
}
```

---

## Feature Highlights

| Feature                                         | Description                                                                          |
| ----------------------------------------------- | ------------------------------------------------------------------------------------ |
| **.NET Compatibility**                          | Works seamlessly with .NET primitives, structs, and classes.                         |
| **High Performance and Low Memory Consumption** | Optimized algorithms ensure fast operations with minimal resource usage.             |
| **Crash Resilience**                            | Robust mechanisms protect data integrity against unexpected failures.                |
| **Efficient Disk Space Utilization**            | Optimized storage strategies minimize disk space requirements.                       |
| **Data Compression**                            | Supports WAL and DiskSegment data compression for efficient storage.                 |
| **Fast Load/Unload**                            | Quickly load and unload large datasets as needed.                                    |
| **Standard CRUD Operations**                    | Provides intuitive and straightforward Create, Read, Update, Delete functionalities. |
| **Optimistic Transactions**                     | Supports concurrent operations with minimal locking overhead.                        |
| **Atomic Read-Modify-Update**                   | Ensures data consistency during complex update operations.                           |
| **In-Memory and Disk Storage**                  | Flexibly operate entirely in memory or persist data to various storage backends.     |
| **Cloud Storage Support**                       | Compatible with cloud-based storage solutions for scalable deployments.              |
| **ACID Compliance**                             | Guarantees Atomicity, Consistency, Isolation, and Durability across transactions.    |
| **Multiple WAL Modes**                          | Choose from four different WAL modes to balance performance and durability.          |
| **Configurable Memory Usage**                   | Adjust the amount of data retained in memory based on application needs.             |
| **Partial and Complete Data Loading**           | Load data partially (with sparse arrays) or completely to and from disk.             |
| **Bidirectional Iteration**                     | Efficiently iterate over data both forward and backward.                             |
| **Optional Dirty Reads**                        | Allow for faster reads when strict consistency is not required.                      |
| **Embeddable Design**                           | Integrate ZoneTree directly into applications without external dependencies.         |
| **SSD Optimization**                            | Tailored for optimal performance on solid-state drives.                              |
| **Exceptionless Transaction API**               | Manage transactions smoothly without relying on exceptions for control flow.         |
| **Fluent Transaction API**                      | Utilize an intuitive, chainable transaction interface with retry capabilities.       |
| **Easy Maintenance**                            | Simplified maintenance processes ensure consistent performance.                      |
| **Configurable LSM Merger**                     | Customize merge operations to suit specific workload patterns.                       |
| **Transparent Implementation**                  | Clear and straightforward codebase reveals internal workings for easy understanding. |
| **Open-Source with MIT License**                | Freely use, modify, and distribute under a permissive license.                       |
| **Transaction Log Compaction**                  | Efficiently manage and reduce the size of transaction logs.                          |
| **Transaction Analysis and Control**            | Analyze and manage transactions for improved performance and reliability.            |
| **Efficient Concurrency Control**               | Minimal overhead through innovative separation of concurrency stamps and data.       |
| **Time-To-Live (TTL) Support**                  | Automatically expire data after a specified duration.                                |
| **Custom Serializer and Comparer Support**      | Implement custom logic for data serialization and comparison.                        |
| **Multiple Disk Segments Mode**                 | Divide data files into configurable chunks for better manageability and performance. |
| **Snapshot Iterators**                          | Create consistent snapshots for data analysis and backup purposes.                   |

---

## ZoneTree.FullTextSearch

[ZoneTree.FullTextSearch](https://www.nuget.org/packages/ZoneTree.FullTextSearch/) is an extension library built upon ZoneTree, providing a **high-performance** and **flexible full-text search engine** for .NET applications.

**Key Features Include:**

- **Fast and Efficient Indexing**: Handles large volumes of text data with impressive speed.
- **Advanced Query Support**: Enables complex search queries with support for Boolean operators and faceted search.
- **Customizable Components**: Allows integration of custom tokenizers, stemmers, and normalizers to suit specific application needs.
- **Scalable Architecture**: Designed to scale seamlessly with growing data and usage demands.

For more information and detailed documentation, visit the [ZoneTree.FullTextSearch GitHub Repository](https://github.com/koculu/ZoneTree.FullTextSearch).

---

## Documentation

Explore comprehensive guides and API references to get the most out of ZoneTree:

- **[Introduction](https://tenray.io/docs/ZoneTree/guide/introduction.html)**
- **[Quick Start Guide](https://tenray.io/docs/ZoneTree/guide/quick-start.html)**
- **[API Documentation](https://tenray.io/docs/ZoneTree/api/Tenray.ZoneTree.html)**
- **[Tuning ZoneTree](https://tenray.io/docs/ZoneTree/guide/tuning-disk-segment.html)**
- **[Features Overview](https://tenray.io/docs/ZoneTree/guide/features.html)**
- **[Terminology](https://tenray.io/docs/ZoneTree/guide/terminology.html)**
- **[Performance Details](https://tenray.io/docs/ZoneTree/guide/performance.html)**

---

## Contributing

Contributions are highly appreciated and welcomed! Here’s how you can help:

1. **Write Tests and Benchmarks**: Improve code reliability and performance analysis.
2. **Enhance Documentation**: Help others understand and utilize ZoneTree effectively.
3. **Submit Feature Requests and Bug Reports**: Share ideas and report issues to refine ZoneTree further.
4. **Optimize Performance**: Contribute optimizations and improvements to existing functionalities.

Please follow the guidelines outlined in **[CONTRIBUTING.md](https://github.com/koculu/ZoneTree/blob/main/.github/CONTRIBUTING.md)** to get started.

---

## License

ZoneTree is licensed under the **[MIT License](https://github.com/koculu/ZoneTree?tab=MIT-1-ov-file#readme)**, allowing for flexible use and distribution.
