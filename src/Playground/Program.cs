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
TestConfig.EnableParalelInserts = true;
TestConfig.DiskSegmentMaximumCachedBlockCount = 1;
TestConfig.DiskCompressionBlockSize = 1024 * 1024 * 10;
TestConfig.WALCompressionBlockSize = 1024 * 32 * 8;
TestConfig.MinimumSparseArrayLength = 0;
TestConfig.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;
ConsoleLogger.DefaultLogLevel = LogLevel.Info;

TestConfig.PrintConfig();

var testCase = 3;

if (testCase == 1)
{
    // bottom segment merger tests.
    var c = 1_000_000_000;
    OldTests.ShowBottomSegments(WriteAheadLogMode.None, c);
    //OldTests.MergeBottomSegment(WriteAheadLogMode.None, c, 3, 6);
    //OldTests.Iterate(WriteAheadLogMode.None, c);
}

if (testCase == 2) {
    // multiple iterator tests.
    var c = 1_000_000;
    var ic = 1000;
    OldTests.Insert(WriteAheadLogMode.None, c);
    OldTests.MultipleIterate(WriteAheadLogMode.None, c, ic);
}

if (testCase == 3)
{
    var b = new Benchmark();
    var test1 = new ZoneTreeTest1();
    var test2 = new ZoneTreeTest2();
    var methods = new CompressionMethod[]
    {
        CompressionMethod.None,
        CompressionMethod.LZ4,
        CompressionMethod.Brotli,
        CompressionMethod.Zstd,
        CompressionMethod.Gzip
    };
    test2.Count = test1.Count = 100_000_000;
    test2.WALMode = test1.WALMode = WriteAheadLogMode.None;

    b.NewSection("int-int insert");
    foreach(var method in methods)
    {
        test1.CompressionMethod = method;
        var stats = b.Run(test1.Insert);
        test1.AddDatabaseFileUsage(stats);
    }

    b.NewSection("str-str insert");
    foreach (var method in methods)
    {
        test2.CompressionMethod = method;
        var stats = b.Run(test2.Insert);
        test2.AddDatabaseFileUsage(stats);
    }

    /*b.NewSection("int-int iterate");
    foreach (var method in methods)
    {
        test1.CompressionMethod = method;
        b.Run(test1.Iterate);
    }

    b.NewSection("str-str iterate");
    foreach (var method in methods)
    {
        test2.CompressionMethod = method;
        b.Run(test2.Iterate);
    }*/

    File.WriteAllText(@"..\..\data\benchmark.json", b.ToJSON());
}