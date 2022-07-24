using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public class ZoneTree1
{
    public static void TestIntTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestIntTree\r\n"); 
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestIntTree";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetMutableSegmentMaxItemCount(1_000_000)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.WriteAheadLogMode = mode;
                x.EnableIncrementalBackup = false;
            })
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        using var basicMaintainer = new BasicZoneTreeMaintainer<int, int>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = 2_000_000;

        Console.WriteLine("Loaded in: " + stopWatch.ElapsedMilliseconds);

        Parallel.For(0, count, (x) =>
        {
            zoneTree.Upsert(x, x + x);
        });

        Console.WriteLine("Completed in: " + stopWatch.ElapsedMilliseconds);
        basicMaintainer.CompleteRunningTasks().Wait();
    }
}
