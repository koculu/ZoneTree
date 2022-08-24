using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Playground.Benchmark;

public class ZoneTree3
{
    const string DataPath = "../../data/";
    const string FolderName = "-transactional-int-int";

    private static string GetDataPath(WriteAheadLogMode mode, int count)
    {
        return DataPath + mode + "-" + count / 1_000_000.0 + "M" + FolderName;
    }

    public static void Insert(WriteAheadLogMode mode, int count)
    {
        var recCount = count / 1000000.0 + "M";
        Console.WriteLine("\r\n--------------------------");
        BenchmarkGroups.LogWithColor($"\r\n{mode} Transaction Insert <int,int> {recCount}\r\n", ConsoleColor.Cyan);

        string dataPath = GetDataPath(mode, count);
        if (TestConfig.RecreateDatabases && Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new ZoneTreeMaintainer<int, int>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;
        basicMaintainer.MinimumSparseArrayLength = TestConfig.MinimumSparseArrayLength;
        BenchmarkGroups.LogWithColor(
            "Loaded in:",
            stopWatch.ElapsedMilliseconds,
            ConsoleColor.DarkYellow);

        if (TestConfig.EnableParalelInserts)
        {
            Parallel.For(0, count, (x) =>
            {
                zoneTree.UpsertAutoCommit(x, x + x);
            });
        }
        else
        {
            for (var x = 0; x < count; ++x)
            {
                zoneTree.UpsertAutoCommit(x, x + x);
            }
        }

        BenchmarkGroups.LogWithColor(
            "Completed in:",
            stopWatch.ElapsedMilliseconds,
            ConsoleColor.Green);
        stopWatch.Restart();

        if (mode == WriteAheadLogMode.None)
        {
            zoneTree.Maintenance.ZoneTree.Maintenance.MoveSegmentZeroForward();
            zoneTree.Maintenance.ZoneTree.Maintenance.StartMergeOperation()?.Join();
        }

        basicMaintainer.CompleteRunningTasks();
        BenchmarkGroups.LogWithColor(
            "Merged in:",
            stopWatch.ElapsedMilliseconds,
            ConsoleColor.DarkCyan);
    }

    public static void Iterate(WriteAheadLogMode mode, int count)
    {
        var recCount = count / 1000000.0 + "M";
        BenchmarkGroups.LogWithColor($"\r\n{mode} Transaction Iterate <int,int> {recCount}\r\n", ConsoleColor.Cyan);

        string dataPath = GetDataPath(mode, count);
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        using var zoneTree = OpenOrCreateZoneTree(mode, dataPath);
        using var basicMaintainer = new ZoneTreeMaintainer<int, int>(zoneTree);
        basicMaintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;
        basicMaintainer.MinimumSparseArrayLength = TestConfig.MinimumSparseArrayLength;
        BenchmarkGroups.LogWithColor(
            "Loaded in:",
            stopWatch.ElapsedMilliseconds,
            ConsoleColor.DarkYellow);

        var off = 0;
        using var iterator = zoneTree.Maintenance.ZoneTree.CreateIterator();
        while (iterator.Next())
        {
            if (iterator.CurrentKey * 2 != iterator.CurrentValue)
                throw new Exception("invalid key or value");
            ++off;
        }
        if (off != count)
            throw new Exception($"missing records. {off} != {count}");

        BenchmarkGroups.LogWithColor(
            "Completed in:",
            stopWatch.ElapsedMilliseconds,
            ConsoleColor.Green);
        basicMaintainer.CompleteRunningTasks();
    }

    private static ITransactionalZoneTree<int, int> OpenOrCreateZoneTree(WriteAheadLogMode mode, string dataPath)
    {
        return new ZoneTreeFactory<int, int>()
            .SetMutableSegmentMaxItemCount(TestConfig.MutableSegmentMaxItemCount)
            .SetDiskSegmentCompression(TestConfig.EnableDiskSegmentCompression)
            .SetDiskSegmentCompressionBlockSize(TestConfig.DiskCompressionBlockSize)
            .SetDiskSegmentMaximumCachedBlockCount(TestConfig.DiskSegmentMaximumCachedBlockCount)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureDiskSegmentOptions(x => x.DiskSegmentMode = TestConfig.DiskSegmentMode)
            .ConfigureWriteAheadLogOptions(x =>
            {
                x.CompressionBlockSize = TestConfig.WALCompressionBlockSize;
                x.WriteAheadLogMode = mode;
                x.EnableIncrementalBackup = TestConfig.EnableIncrementalBackup;
            })
            .ConfigureTransactionLog(x =>
            {
                x.CompactionThreshold = 100_000;
            })
            .SetInitialSparseArrayLength(TestConfig.MinimumSparseArrayLength)
            .OpenOrCreateTransactional();
    }
}
