using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BplusTree;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.UnitTests;

public class BplusTreeThreadSafetyTests
{
    [TestCase(3_000_000)]
    public void MassiveInsertsAndReads(int count)
    {
        var readCount = 0;
        var tree = new SafeBplusTree<long, long>(new Int64ComparerAscending());
        var task1 = Parallel.ForEachAsync(Enumerable.Range(0, count), (i, t) =>
        {
            tree.TryInsert(i, i);
            return ValueTask.CompletedTask;
        });
        Thread.Sleep(1);
        var task2 = Parallel.ForEachAsync(Enumerable.Range(0, count), (i, t) =>
        {
            if (tree.TryGetValue(i, out var j))
            {
                if (i != j)
                    throw new Exception($"{i} != {j}");
                Interlocked.Increment(ref readCount);
            }
            return ValueTask.CompletedTask;
        });
        task1.Wait();
        task2.Wait();
        Console.WriteLine("Read Count: " + readCount);
    }

    [TestCase(3_000_000)]
    public void MassiveInsertsAndReadsSkiplist(int count)
    {
        var readCount = 0;
        var tree = new SkipList<long, long>(new Int64ComparerAscending());
        var task1 = Parallel.ForEachAsync(Enumerable.Range(0, count), (i, t) =>
        {
            tree.TryInsert(i, i);
            return ValueTask.CompletedTask;
        });
        Thread.Sleep(1);
        var task2 = Parallel.ForEachAsync(Enumerable.Range(0, count), (i, t) =>
        {
            if (tree.TryGetValue(i, out var j))
            {
                if (i != j)
                    throw new Exception($"{i} != {j}");
                Interlocked.Increment(ref readCount);
            }
            return ValueTask.CompletedTask;
        });
        task1.Wait();
        task2.Wait();
        Console.WriteLine("Read Count: " + readCount);
    }
}