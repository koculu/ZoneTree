using BenchmarkDotNet.Running;
using Playground.Benchmark;
using Playground.InMemoryTreeBenchmark;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.WAL;

TestConfig.EnableIncrementalBackup = true;
TestConfig.MutableSegmentMaxItemCount = 1_000_000;
TestConfig.ThresholdForMergeOperationStart = 20_000_000;
TestConfig.RecreateDatabases = true;
TestConfig.EnableParalelInserts = true;
TestConfig.DiskSegmentMaximumCachedBlockCount = 8;
TestConfig.DiskCompressionBlockSize = 1024 * 1024;
TestConfig.WALCompressionBlockSize = 1024 * 32;
TestConfig.MinimumSparseArrayLength = 0;
TestConfig.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;

//BenchmarkGroups.Insert1(1_000_000, WriteAheadLogMode.Lazy);


/*BenchmarkRunner.Run<InMemoryIntTreeBenchmark>();
BenchmarkRunner.Run<InMemoryLongTreeBenchmark>();
BenchmarkRunner.Run<InMemoryMidSizeTreeBenchmark>();
BenchmarkRunner.Run<InMemoryBigKeyTreeBenchmark>();*/

RandomLongInserts.InsertBplusTree(RandomLongInserts.GetRandomArray(1_000_000));

/*
var c = 10_000_000;
var m = 1_000_000;
var a = 10000;
BenchmarkGroups.Insert1(c);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Lazy, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.CompressedImmediate, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Immediate, c, m, a);
BenchmarkGroups.Iterate1(c);
*/