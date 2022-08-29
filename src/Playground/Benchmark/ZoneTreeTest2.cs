using Humanizer;
using System.Diagnostics;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Playground.Benchmark;

public class ZoneTreeTest2 : ZoneTreeTestBase
{
    const string FolderName = "-str-str";

    public override string DataPath => 
        RootPath + WALMode + "-" + Count.ToHuman()
            + "_" + CompressionMethod + "_"
            + FolderName;

    public void Insert(IStatsCollector stats)
    {
        stats.Name = "Insert";
        AddOptions(stats);
        var count = Count;
        stats.LogWithColor(GetLabel("Insert <str, str>"), ConsoleColor.Cyan);

        if (TestConfig.RecreateDatabases && Directory.Exists(DataPath))
            Directory.Delete(DataPath, true);

        stats.RestartStopwatch();

        using var zoneTree = OpenOrCreateZoneTree<string, string>();
        using var maintainer = CreateMaintainer(zoneTree);
        stats.AddStage("Loaded In");

        if (TestConfig.EnableParalelInserts)
        {
            Parallel.For(0, count, (x) =>
            {
                var str = "abcdefghijklmno" + x;
                zoneTree.Upsert(str, str);
            });
        }
        else
        {
            for (var x = 0; x < count; ++x)
            {
                var str = "abcdefghijklmno" + x;
                zoneTree.Upsert(str, str);
            }
        }

        stats.AddStage("Inserted In", ConsoleColor.Green);

        if (WALMode == WriteAheadLogMode.None)
        {
            zoneTree.Maintenance.MoveMutableSegmentForward();
            zoneTree.Maintenance.StartMergeOperation()?.Join();
        }
        maintainer.CompleteRunningTasks();

        stats.AddStage("Merged In", ConsoleColor.DarkCyan);
    }

    public void Iterate(IStatsCollector stats)
    {
        stats.Name = "Iterate";
        var count = Count;
        stats.LogWithColor(GetLabel("Iterate <str, str>"), ConsoleColor.Cyan);
        stats.RestartStopwatch();

        using var zoneTree = OpenOrCreateZoneTree<string, string>();
        using var maintainer = CreateMaintainer(zoneTree);

        stats.AddStage("Loaded in", ConsoleColor.DarkYellow);

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

        stats.AddStage(
            "Iterated in",
            ConsoleColor.Green);
        maintainer.CompleteRunningTasks();
    }
}
