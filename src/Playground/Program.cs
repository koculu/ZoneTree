using BenchmarkDotNet.Running;
using Playground;
using Playground.Benchmark;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.WAL;

var summary = BenchmarkRunner.Run<ZoneTreeBenchmarks>(); return;

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
TestConfig.EnableParalelInserts = false;
TestConfig.DiskSegmentMaximumCachedBlockCount = 8;
TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 1;
TestConfig.WALCompressionBlockSize = 1024 * 1024;
TestConfig.MinimumSparseArrayLength = 0;
TestConfig.DiskSegmentMode = DiskSegmentMode.MultipleDiskSegments;

BenchmarkGroups.InsertIterate1(100_000_000, WriteAheadLogMode.Lazy);
