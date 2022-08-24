using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Playground.Benchmark;

[HtmlExporter]
[SimpleJob(RunStrategy.ColdStart, targetCount: 1)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn, /*AllStatisticsColumn*/]
[RPlotExporter]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(
    HardwareCounter.TotalCycles,
    HardwareCounter.TotalIssues,
    HardwareCounter.CacheMisses,
    HardwareCounter.Timer)]
public class ZoneTreeBenchmarks
{
    [GlobalSetup]
    public void Setup()
    {
        TestConfig.EnableParalelInserts = false;
        TestConfig.DiskSegmentMaximumCachedBlockCount = 8;
        TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 1;
        TestConfig.WALCompressionBlockSize = 1024 * 1024;
        TestConfig.MinimumSparseArrayLength = 0;
        TestConfig.DiskSegmentMode = DiskSegmentMode.MultiPartDiskSegment;
    }

    [Benchmark]
    public void Insert_1M_Lazy() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.AsyncCompressed);

    [Benchmark]
    public void Insert_3M1_Lazy() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.AsyncCompressed);

    [Benchmark]
    public void Insert_1M_Immediate() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.Sync);

    [Benchmark]
    public void Insert_3M_Immediate() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.Sync);

    [Benchmark]
    public void Insert_1M_CompressedImmediate() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.SyncCompressed);

    [Benchmark]
    public void Insert_3M_CompressedImmediate() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.SyncCompressed);

}