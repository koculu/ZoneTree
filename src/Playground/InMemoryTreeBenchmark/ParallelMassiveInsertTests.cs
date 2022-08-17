using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Comparers;

namespace Playground.InMemoryTreeBenchmark;

[HtmlExporter]
[SimpleJob(RunStrategy.ColdStart, targetCount: 1)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, /*AllStatisticsColumn*/]
[MemoryDiagnoser]
/*[HardwareCounters(
    HardwareCounter.TotalCycles,
    HardwareCounter.TotalIssues,
    HardwareCounter.CacheMisses,
    HardwareCounter.Timer)]*/
public class ParallelMassiveInsertTests
{
    readonly int Count = 3_000_000;
    readonly bool Shuffled = true;

    static readonly BTreeLockMode BTreeLockMode = BTreeLockMode.NodeLevelMonitor;

    [GlobalSetup]
    public void Setup()
    {
        Data = Shuffled ?
            RandomLongInserts.GetRandomArray(Count) :
            RandomLongInserts.GetSortedArray(Count);
    }

    long[] Data = Array.Empty<long>();

    [Benchmark]
    public void Parallel_BTree() => MassiveInsertsAndReadsBTree();

    public void MassiveInsertsAndReadsBTree()
    {
        var tree = new BTree<long, long>(new Int64ComparerAscending(), BTreeLockMode);
        var task1 = Parallel.ForEachAsync(Enumerable.Range(0, Count), (i, t) =>
        {
            tree.TryInsert(i, i, out _);
            return ValueTask.CompletedTask;
        });
        var task2 = Parallel.ForEachAsync(Enumerable.Range(0, Count), (i, t) =>
        {
            tree.TryGetValue(i, out var j);
            return ValueTask.CompletedTask;
        });
        task1.Wait();
        task2.Wait();
    }
}