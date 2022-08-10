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
public class InMemoryBigKeyTreeBenchmark
{
    readonly int Count = 1_000_000;
    readonly bool Shuffled = true;

    [GlobalSetup]
    public void Setup()
    {
        Data = Shuffled ?
            RandomBigInserts.GetRandomArray(Count) :
            RandomBigInserts.GetSortedArray(Count);
    }


    BigKey[] Data = Array.Empty<BigKey>();

    [Benchmark]
    public void InsertBplusTree() => RandomBigInserts.InsertBplusTree(Data);

    [Benchmark]
    public void InsertSkipList() => RandomBigInserts.InsertSkipList(Data);

    [Benchmark]
    public void InsertSortedDictionary() => RandomBigInserts.InsertSortedDictionary(Data);

}