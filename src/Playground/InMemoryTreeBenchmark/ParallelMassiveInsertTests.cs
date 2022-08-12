using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Comparers;

namespace Playground.InMemoryTreeBenchmark;

[HtmlExporter]
[SimpleJob(RunStrategy.ColdStart, targetCount: 1)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, /*AllStatisticsColumn*/]
[MemoryDiagnoser]
[HardwareCounters(
    HardwareCounter.TotalCycles,
    HardwareCounter.TotalIssues,
    HardwareCounter.CacheMisses,
    HardwareCounter.Timer)]
public class ParallelMassiveInsertTests
{
    readonly int Count = 3_000_000;
    readonly bool Shuffled = true;

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

    [Benchmark]
    public void Parallel_SkipList() => MassiveInsertsAndReadsSkiplist();

    public void MassiveInsertsAndReadsBTree()
    {
        var tree = new BTree<long, long>(new Int64ComparerAscending());
        var task1 = Parallel.ForEachAsync(Enumerable.Range(0, Count), (i, t) =>
        {
            tree.TryInsert(i, i);
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

    public void MassiveInsertsAndReadsSkiplist()
    {
        var tree = new SkipList<long, long>(new Int64ComparerAscending());
        var task1 = Parallel.ForEachAsync(Data, (i, t) =>
        {
            tree.TryInsert(i, i);
            return ValueTask.CompletedTask;
        });
        var task2 = Parallel.ForEachAsync(Data, (i, t) =>
        {
            tree.TryGetValue(i, out var j);
            return ValueTask.CompletedTask;
        });
        task1.Wait();
        task2.Wait();
    }
}