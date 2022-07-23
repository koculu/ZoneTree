using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Maintainers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground;

public class Test1
{
    public void Run()
    {
        var dataPath = "../../data/SeveralParallelTransactions";
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        int n = 1000000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = WriteAheadLogMode.Lazy)
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
        basicMaintainer.CompleteRunningTasks().AsTask().Wait();
        zoneTree.Maintenance.SaveMetaData();
    }
}