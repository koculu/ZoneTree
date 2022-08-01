using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

var custom = false;
if (custom)
{
    TestConfig.EnableIncrementalBackup = true;
    TestConfig.RecreateDatabases = true;
    TestConfig.MutableSegmentMaxItemCount = 100000;
    TestConfig.ThresholdForMergeOperationStart = 300000;
    TestConfig.WALCompressionBlockSize = 16384;
}

var testAll = true;

if (testAll)
{
    BenchmarkGroups.InsertIterate1();
}
else
{
    BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.Lazy);
    BenchmarkGroups.Iterate1(3_000_000, WriteAheadLogMode.Lazy);
}
