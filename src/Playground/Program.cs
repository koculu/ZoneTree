using BenchmarkDotNet.Running;
using Playground;
using Playground.Benchmark;
using Tenray.ZoneTree.Core;
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
TestConfig.EnableParalelInserts = false;
TestConfig.DiskSegmentMaximumCachedBlockCount = 8;
TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 1;
TestConfig.WALCompressionBlockSize = 1024 * 1024;
TestConfig.MinimumSparseArrayLength = 0;
TestConfig.DiskSegmentMode = DiskSegmentMode.MultipleDiskSegments;

var c = 10_000_000;
var m = 1_000_000;
var a = 10000;
BenchmarkGroups.Insert1(c);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Lazy, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.CompressedImmediate, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Immediate, c, m, a);
BenchmarkGroups.Iterate1(c);