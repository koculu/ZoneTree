﻿using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using Playground;
using Playground.Benchmark;
using Playground.InMemoryTreeBenchmark;
using Tenray.ZoneTree.Core;
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
ConsoleLogger.DefaultLogLevel = LogLevel.Warning;
//BenchmarkRunner.Run<ParallelMassiveInsertTests>();
//BenchmarkGroups.Iterate3(3_000_000, WriteAheadLogMode.CompressedImmediate)
//RecoverFile.Recover2();
/*
while(true)
    BenchmarkGroups.InsertIterate3(3_000_000, WriteAheadLogMode.CompressedImmediate);
*/

//Test1.TestTreeIteratorBehavior();

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
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Lazy, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.CompressedImmediate, c, m, a);
ZoneTree1.InsertSingleAndMerge(WriteAheadLogMode.Immediate, c, m, a);
BenchmarkGroups.Iterate1(c);
*/