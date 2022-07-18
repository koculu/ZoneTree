using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenray;
using ZoneTree.Serializers;

namespace Playground;

public class Test1
{
    public void Run()
    {
        var dataPath = "data/SeveralParallelTransactions";
        /*if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);*/
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        int n = 100000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreateTransactional();

        Console.WriteLine("Loaded: " + stopWatch.ElapsedMilliseconds);
        Parallel.For(0, n, (x) =>
        {
            var tx = zoneTree.BeginTransaction();
            zoneTree.Upsert(tx, x, x + x);
            zoneTree.Upsert(tx, -x, -x - x);
            zoneTree.Prepare(tx);
            zoneTree.Commit(tx);
        });
        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
        //zoneTree.DestroyTree();
    }
}
