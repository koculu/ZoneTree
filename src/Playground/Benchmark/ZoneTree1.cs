using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;
public class ZoneTree1
{
    public static void TestInsertIntTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestIntTree\r\n");
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestIntTree" + mode + count;
        if (TestConfig.RecreateDatabases && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new BasicZoneTreeMaintainer<int, int>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;

        Console.WriteLine("Loaded in: " + stopWatch.ElapsedMilliseconds);

        Parallel.For(0, count, (x) =>
        {
            zoneTree.Upsert(x, x + x);
        });

        Console.WriteLine("Completed in: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks().Wait();
    }

    public static void TestIterateIntTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestIterationIntTree\r\n");
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestIntTree" + mode + count;
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new BasicZoneTreeMaintainer<int, int>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;

        Console.WriteLine("Loaded in: " + stopWatch.ElapsedMilliseconds);

        var off = 0;
        using var iterator = zoneTree.CreateIterator();
        while (iterator.Next())
        {
            if (iterator.CurrentKey * 2 != iterator.CurrentValue)
                throw new Exception("invalid key or value");
            ++off;
        }
        if (off != count)
            throw new Exception($"missing records. {off} != {count}");

        Console.WriteLine("Completed in: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks().Wait();
    }

    private static IZoneTree<int, int> OpenOrCreateZoneTree(WriteAheadLogMode mode, string dataPath)
    {
        return new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetMutableSegmentMaxItemCount(TestConfig.MutableSegmentMaxItemCount)
            .SetDiskSegmentCompression(TestConfig.EnableDiskSegmentCompression)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.CompressionBlockSize = TestConfig.WALCompressionBlockSize;
                x.WriteAheadLogMode = mode;
                x.EnableIncrementalBackup = TestConfig.EnableIncrementalBackup;
            })
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
    }
}
