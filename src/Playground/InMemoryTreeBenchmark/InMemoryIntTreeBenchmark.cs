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
public class InMemoryIntTreeBenchmark
{
    readonly int Count = 1_000_000;
    readonly bool Shuffled = true;

    [GlobalSetup]
    public void Setup()
    {
        Data = Shuffled ?
            RandomIntInserts.GetRandomArray(Count) : 
            RandomIntInserts.GetSortedArray(Count);
    }


    int[] Data = Array.Empty<int>();

    [Benchmark]
    public void InsertBplusTree() => RandomIntInserts.InsertBplusTree(Data);

    [Benchmark]
    public void InsertSkipList() => RandomIntInserts.InsertSkipList(Data);

    [Benchmark]
    public void InsertSortedDictionary() => RandomIntInserts.InsertSortedDictionary(Data);

}