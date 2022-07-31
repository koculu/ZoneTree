using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public class ZoneTree2
{
    public static void TestInsertStringTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestStringTree\r\n");
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestStringTree" + mode + count;
        if (TestConfig.RecreateDatabases && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new BasicZoneTreeMaintainer<string, string>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;
        Console.WriteLine("Loaded in: " + stopWatch.ElapsedMilliseconds);

        Parallel.For(0, count, (x) =>
        {
            var str = "abcdefghijklmno" + x;
            zoneTree.Upsert(str, str);
        });

        Console.WriteLine("Completed in: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks().Wait();
        Console.WriteLine("\r\n-------------------------\r\n");
    }

    public static void TestIterateStringTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestStringTree\r\n");
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestStringTree" + mode + count;

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new BasicZoneTreeMaintainer<string, string>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;
        Console.WriteLine("Loaded in: " + stopWatch.ElapsedMilliseconds);

        var off = 0;
        using var iterator = zoneTree.CreateIterator();
        while (iterator.Next())
        {
            if (iterator.CurrentKey != iterator.CurrentValue)
                throw new Exception("invalid key or value");
            ++off;
        }
        if (off != count)
            throw new Exception($"missing records. {off} != {count}");

        Console.WriteLine("Completed in: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks().Wait();
        Console.WriteLine("\r\n-------------------------\r\n");
    }

    private static IZoneTree<string, string> OpenOrCreateZoneTree(WriteAheadLogMode mode, string dataPath)
    {
        return new ZoneTreeFactory<string, string>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetMutableSegmentMaxItemCount(TestConfig.MutableSegmentMaxItemCount)
            .SetDiskSegmentCompression(TestConfig.EnableDiskSegmentCompression)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.CompressionBlockSize = TestConfig.WALCompressionBlockSize;
                x.WriteAheadLogMode = mode;
                x.EnableIncrementalBackup = false;
            })
            .SetKeySerializer(new Utf8StringSerializer())
            .SetValueSerializer(new Utf8StringSerializer())
            .OpenOrCreate();
    }
}
