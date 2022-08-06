using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

var custom = false;
if (custom)
{
    TestConfig.EnableIncrementalBackup = true;
    TestConfig.RecreateDatabases = false;
    TestConfig.MutableSegmentMaxItemCount = 100000;
    TestConfig.ThresholdForMergeOperationStart = 300000;
    TestConfig.WALCompressionBlockSize = 1024 * 1024 * 1;
    TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 100;
}

TestConfig.DiskSegmentMaximumCachedBlockCount = 32;
TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 1;
TestConfig.WALCompressionBlockSize = 32768;
TestConfig.MinimumSparseArrayLength = 1_000;
var testAll = true;

if (testAll)
{
    BenchmarkGroups.InsertIterate2(0);
}
else
{
    BenchmarkGroups.Insert1(3_000_000, WriteAheadLogMode.Lazy);
    BenchmarkGroups.Iterate1(3_000_000, WriteAheadLogMode.Lazy);
}
