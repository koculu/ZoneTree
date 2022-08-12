using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BplusTree.Lock;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.UnitTests;

public class BTreeThreadSafetyTests
{
    [TestCase(3_000_000, BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(3_000_000, BTreeLockMode.TopLevelMonitor)]
    [TestCase(3_000_000, BTreeLockMode.NodeLevelMonitor)]
    [TestCase(3_000_000, BTreeLockMode.NodeLevelReaderWriter)]
    public void MassiveInsertsAndReads(int count, BTreeLockMode lockMode)
    {
        var readCount = 0;
        var tree = new BTree<long, long>(new Int64ComparerAscending(), lockMode);
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