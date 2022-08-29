using BenchmarkDotNet.Running;
using Playground;
using Playground.Benchmark;
using Playground.InMemoryTreeBenchmark;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.WAL;

TestConfig.EnableIncrementalBackup = false;
TestConfig.MutableSegmentMaxItemCount = 1_000_000;
TestConfig.ThresholdForMergeOperationStart = 2_000_000;
TestConfig.RecreateDatabases = true;
TestConfig.EnableParalelInserts = false;
TestConfig.DiskSegmentMaximumCachedBlockCount = 1;
TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 10;
TestConfig.WALCompressionBlockSize = 1024 * 32 * 8;
TestConfig.MinimumSparseArrayLength = 0;
TestConfig.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;
ConsoleLogger.DefaultLogLevel = LogLevel.Info;

TestConfig.PrintConfig();

if (false)
{
    // bottom segment merger tests.
    var c = 1_000_000_000;
    ZoneTree1.ShowBottomSegments(WriteAheadLogMode.None, c);
    //ZoneTree1.MergeBottomSegment(WriteAheadLogMode.None, c, 3, 6);
    //ZoneTree1.Iterate(WriteAheadLogMode.None, c);
}

if (false) {
    // multiple iterator tests.
    var c = 1_000_000;
    var ic = 1000;
    ZoneTree1.Insert(WriteAheadLogMode.None, c);
    ZoneTree1.MultipleIterate(WriteAheadLogMode.None, c, ic);
}

if (true)
{
    BenchmarkGroups.InsertIterate1(0);
    BenchmarkGroups.InsertIterate2(0);
    BenchmarkGroups.InsertIterate3(0);
}