using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public class ZoneTree2
{
    public static void TestStringTree(WriteAheadLogMode mode, int count)
    {
        Console.WriteLine("\r\nTestStringTree\r\n");
        Console.WriteLine("Record count = " + count);
        Console.WriteLine("WriteAheadLogMode: = " + mode);

        var dataPath = "../../data/TestStringTree";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = new ZoneTreeFactory<string, string>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x =>
            {
                x.WriteAheadLogMode = mode;
                x.EnableIncrementalBackup = false;
            })
            .SetKeySerializer(new Utf8StringSerializer())
            .SetValueSerializer(new Utf8StringSerializer())
            .OpenOrCreate();
        using var basicMaintainer = new BasicZoneTreeMaintainer<string, string>(zoneTree);

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
}
