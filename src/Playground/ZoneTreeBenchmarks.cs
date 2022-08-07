using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

namespace Playground;



//[SimpleJob(RuntimeMoniker.CoreRt60, baseline: true)]
[HardwareCounters(
    HardwareCounter.BranchMispredictions,
    HardwareCounter.BranchInstructions)]
[RPlotExporter]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class ZoneTreeBenchmarks
{
    [Benchmark]
    public void Insert_1M_Lazy() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.Lazy);
    
    [Benchmark]
    public void Insert_3M1_Lazy() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.Lazy);
    
    [Benchmark]
    public void Insert_1M_Immediate() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.Immediate);
    
    [Benchmark]
    public void Insert_3M_Immediate() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.Immediate);
    
    [Benchmark]
    public void Insert_1M_CompressedImmediate() => BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.CompressedImmediate);
    
    [Benchmark]
    public void Insert_3M_CompressedImmediate() => BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.CompressedImmediate);
    
}