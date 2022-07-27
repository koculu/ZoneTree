using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

TestConfig.RecreateDatabases = false;
bool testAll = true;
if (testAll)
{
    BenchmarkGroups.InsertBenchmark1();
    BenchmarkGroups.LoadAndIterateBenchmark1();
}
else
{
    BenchmarkGroups.InsertBenchmark1(1_000_000, WriteAheadLogMode.CompressedImmediate);
    BenchmarkGroups.LoadAndIterateBenchmark1(1_000_000, WriteAheadLogMode.CompressedImmediate);
}
