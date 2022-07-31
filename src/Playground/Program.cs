using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

var custom = false;
if (custom)
{
    TestConfig.RecreateDatabases = true;
    TestConfig.MutableSegmentMaxItemCount = 100000;
    TestConfig.ThresholdForMergeOperationStart = 300000;
    TestConfig.WALCompressionBlockSize = 16384;
}

var testAll = true;

if (testAll)
{
    BenchmarkGroups.InsertBenchmark3();
    BenchmarkGroups.LoadAndIterateBenchmark3();
}
else
{
    BenchmarkGroups.InsertBenchmark1(3_000_000, WriteAheadLogMode.Lazy);
    BenchmarkGroups.LoadAndIterateBenchmark1(3_000_000, WriteAheadLogMode.Lazy);
}
