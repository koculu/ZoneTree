using BenchmarkDotNet.Running;
using Playground;
using Playground.Benchmark;
using Playground.InMemoryTreeBenchmark;
using Tenray.ZoneTree;
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
//BenchmarkRunner.Run<ParallelMassiveInsertTests>();
//BenchmarkGroups.Iterate3(3_000_000, WriteAheadLogMode.SyncCompressed)
//RecoverFile.Recover2();
/*
while(true)
    BenchmarkGroups.InsertIterate3(3_000_000, WriteAheadLogMode.SyncCompressed);
*/

//Test1.TestTreeIteratorBehavior();
/*var c = 1_000_000;
var ic = 1000;
ZoneTree1.Insert(WriteAheadLogMode.None, c);
ZoneTree1.MultipleIterate(WriteAheadLogMode.None, c, ic);*/

//BenchmarkGroups.InsertIterate1(10_000_000, WriteAheadLogMode.None);

BenchmarkGroups.InsertIterate1(0);
BenchmarkGroups.InsertIterate2(0);
BenchmarkGroups.InsertIterate3(0);

//Test1.MassiveInsertsAndReads(2_000_000);
//Test1.BTreeReverseIteratorParallelInserts();
//for (var i = 0; i < 45000; ++i)
//    RandomIntInserts.InsertBTree(RandomIntInserts.GetSortedArray(i));
/*

BenchmarkRunner.Run<ParallelMassiveInsertTests>();
BenchmarkRunner.Run<IntTreeBenchmark>();
BenchmarkRunner.Run<LongTreeBenchmark>();
BenchmarkRunner.Run<MidSizeTreeBenchmark>();
BenchmarkRunner.Run<BigKeyTreeBenchmark>();
*/

/*
var c = 10_000_000;
var m = 1_000_000;
var a = 10000;
BenchmarkGroups.Insert1(c);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.AsyncCompressed, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.SyncCompressed, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Sync, c, m, a);
BenchmarkGroups.Iterate1(c);
*/