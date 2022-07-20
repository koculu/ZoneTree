using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenray;
using ZoneTree.Maintainers;
using ZoneTree.Serializers;
using ZoneTree.WAL;

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
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreateTransactional();
        using var basicMaintainer = new BasicZoneTreeMaintainer<int, int>(zoneTree);

        Console.WriteLine("Loaded: " + stopWatch.ElapsedMilliseconds);

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
        zoneTree.Maintenance.SaveMetaData();
        basicMaintainer.CompleteRunningTasks().AsTask().Wait();
    }
}