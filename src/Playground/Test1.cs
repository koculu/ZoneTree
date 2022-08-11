using NUnit.Framework;
using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground;

public class Test1
{
    public static void SeveralParallelTransactions()
    {
        var dataPath = "../../data/SeveralParallelTransactions";
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        int n = 1000000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.WriteAheadLogMode = WriteAheadLogMode.Immediate;
                x.EnableIncrementalBackup = true;
            })
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreateTransactional();
        using var basicMaintainer = new BasicZoneTreeMaintainer<int, int>(zoneTree);
        
        Console.WriteLine("Loaded: " + stopWatch.ElapsedMilliseconds);

        stopWatch.Restart();
        var uncommitted = zoneTree.Maintenance.TransactionLog.UncommittedTransactionIds;
        Console.WriteLine($"found {uncommitted.Count} uncommitted transactions.");
        foreach (var u in uncommitted)
        {
            zoneTree.Rollback(u);
        }
        Console.WriteLine("uncommitted transactions are rollbacked. Elapsed:"
            + stopWatch.ElapsedMilliseconds);
        stopWatch.Restart();

        var transactional = true;
        if (transactional)
        {
            Parallel.For(0, n, (x) =>
            {
                var tx = zoneTree.BeginTransaction();
                zoneTree.Upsert(tx, x, x + x);
                zoneTree.Upsert(tx, -x, -x - x);
                zoneTree.Prepare(tx);
                zoneTree.Commit(tx);
            });
        }
        else
        {
            var data = zoneTree.Maintenance.ZoneTree;
            Parallel.For(0, n, (x) =>
            {
                data.Upsert(x, x + x);
                data.Upsert(-x, -x - x);
            });
        }
        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks();
        zoneTree.Maintenance.SaveMetaData();
    }

    public static void TestReverseIterator(
        int count = 1_000_000,
        bool recreate = false)
    {
        var dataPath = "../../data/TestReverseIterator";
        if (recreate && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopwatchAll = Stopwatch.StartNew();

        var upload = Stopwatch.StartNew();
        IZoneTree<int, string> GetZoneTree() => new ZoneTreeFactory<int, string>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.CompressionBlockSize = 1024 * 1024 * 20;
                x.WriteAheadLogMode = WriteAheadLogMode.Lazy;
            })
            .Configure(x =>
            {
                x.EnableDiskSegmentCompression = true;
                x.DiskSegmentMode = DiskSegmentMode.MultipleDiskSegments;
                x.DiskSegmentCompressionBlockSize = 1024 * 1024 * 20;
                x.DiskSegmentMinimumRecordCount = 10_000;
                x.DiskSegmentMaximumRecordCount = 100_000;
            })
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Utf8StringSerializer())
            .OpenOrCreate();

        using (var zoneTree1 = GetZoneTree())
        {
            using var maintainer = 
                new BasicZoneTreeMaintainer<int, string>(zoneTree1);
            maintainer.ThresholdForMergeOperationStart = 300000;
            for (int i = 0; i < 1_000_000; i++)
            {
                zoneTree1.Upsert(i, string.Join('+',
                    Enumerable.Repeat(Guid.NewGuid().ToString(), 4)));
                if (i % 100 == 0)
                    zoneTree1.TryAtomicAddOrUpdate(i, "a", (x) => x + " ooops!");
            }
            maintainer.CompleteRunningTasks();
        }

        upload.Stop();
        Console.WriteLine($"Upload {upload.Elapsed}");

        var download = Stopwatch.StartNew();
        int iterateCount = 0;
        using (var zoneTree2 = GetZoneTree())
        {
            using var iterator = zoneTree2.CreateIterator();
            while (iterator.Next())
            {
                var key = iterator.CurrentKey;
                var value = iterator.CurrentValue;
                if (value.Length > 0)
                    iterateCount++;
            }
        }
        download.Stop();
        Console.WriteLine($"Download {download.Elapsed}");

        var reverse = Stopwatch.StartNew();

        using (var zoneTree3 = GetZoneTree())
        {
            using var reverseIterator = zoneTree3.CreateReverseIterator();
            while (reverseIterator.Next())
            {
                var key = reverseIterator.CurrentKey;
                var value = reverseIterator.CurrentValue;
                if (value.Length > 0)
                    iterateCount++;
            }
        }
        reverse.Stop();
        Console.WriteLine($"Reverse {reverse.Elapsed}");

        if (2 * count != iterateCount)
        {
            throw new Exception($"iterateCount != {2*count} " + iterateCount);
        }
        stopwatchAll.Stop();
        Console.WriteLine($"All Time:{stopwatchAll.Elapsed}");
    }

    public static void BplusTreeReverseIteratorParallelInserts()
    {
        var random = new Random();
        var insertCount = 100000;
        var iteratorCount = 1000;

        var tree = new SafeBplusTree<int, int>(
            new Int32ComparerAscending());

        var task = Task.Factory.StartNew(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next();
                tree.AddOrUpdate(key,
                    AddOrUpdateResult (ref int x) =>
                    {
                        x = key + key;
                        return AddOrUpdateResult.ADDED;
                    },
                    AddOrUpdateResult (ref int y) =>
                    {
                        y = key + key;
                        return AddOrUpdateResult.UPDATED;
                    });
            });
        });
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = tree.Length;
            var iterator = new SafeBplusTreeSeekableIterator<int, int>(tree);
            var counter = iterator.SeekEnd() ? 1 : 0;
            var isValidData = true;
            while (iterator.Prev())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);

            initialCount = tree.Length;
            counter = iterator.SeekBegin() ? 1 : 0;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });

        task.Wait();
    }
}