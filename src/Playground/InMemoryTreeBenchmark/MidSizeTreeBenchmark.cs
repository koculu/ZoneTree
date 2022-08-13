using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;

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
public class MidSizeTreeBenchmark
{
    readonly int Count = 1_000_000;
    readonly bool Shuffled = true;

    [GlobalSetup]
    public void Setup()
    {
        Data = Shuffled ?
            RandomMidSizeInserts.GetRandomArray(Count) :
            RandomMidSizeInserts.GetSortedArray(Count);
    }

    MidSizeKey[] Data = Array.Empty<MidSizeKey>();

    [Benchmark]
    public void InsertBTree() => RandomMidSizeInserts.InsertBTree(Data);

    [Benchmark]
    public void InsertSkipList() => RandomMidSizeInserts.InsertSkipList(Data);

    [Benchmark]
    public void InsertSortedDictionary() => RandomMidSizeInserts.InsertSortedDictionary(Data);

}