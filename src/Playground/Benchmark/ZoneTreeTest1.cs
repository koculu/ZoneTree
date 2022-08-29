using Humanizer;
using System.Diagnostics;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Playground.Benchmark;

public class ZoneTreeTest1 : ZoneTreeTestBase
{
    const string FolderName = "-int-int";

    public override string DataPath => 
        RootPath + WALMode + "-" + Count.ToHuman()
            + "_" + CompressionMethod + "_"
            + FolderName;

    public void Insert(IStatsCollector stats)
    {
        stats.Name = "Insert";
        AddOptions(stats);
        var count = Count;
        stats.LogWithColor(GetLabel("Insert <int, int>"), ConsoleColor.Cyan);

        if (TestConfig.RecreateDatabases && Directory.Exists(DataPath))
            Directory.Delete(DataPath, true);

        stats.RestartStopwatch();

        using var zoneTree = OpenOrCreateZoneTree<int, int>();
        using var maintainer = CreateMaintainer(zoneTree);
        stats.AddStage("Loaded In");

        if (TestConfig.EnableParalelInserts)
        {
            Parallel.For(0, count, (x) =>
            {
                zoneTree.Upsert(x, x + x);
            });
        }
        else
        {
            for (var x = 0; x < count; ++x)
            {
                zoneTree.Upsert(x, x + x);
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
        stats.LogWithColor(GetLabel("Iterate <int, int>"), ConsoleColor.Cyan);
        stats.RestartStopwatch();

        using var zoneTree = OpenOrCreateZoneTree<int,int>();
        using var maintainer = CreateMaintainer(zoneTree);

        stats.AddStage("Loaded in", ConsoleColor.DarkYellow);

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

        stats.AddStage(
            "Iterated in",
            ConsoleColor.Green);
        maintainer.CompleteRunningTasks();
    }
}
