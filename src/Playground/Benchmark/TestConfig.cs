using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Playground.Benchmark;

public static class TestConfig
{
    public static bool RecreateDatabases = true;

    public static int ThresholdForMergeOperationStart = 2_000_000;

    public static int MutableSegmentMaxItemCount = 1_000_000;

    public static bool EnableIncrementalBackup = true;

    public static bool EnableDiskSegmentCompression = true;

    public static int WALCompressionBlockSize = 32768;

    public static int DiskCompressionBlockSize = 32768;

    public static int DiskSegmentMaximumCachedBlockCount = 1000;

    public static int MinimumSparseArrayLength = 1_000_000;

    public static bool EnableParalelInserts = false;

    public static DiskSegmentMode DiskSegmentMode = DiskSegmentMode.MultiPartDiskSegment;

    public static void PrintConfig()
    {
        Console.WriteLine($"ThresholdForMergeOperationStart: {ThresholdForMergeOperationStart}");
        Console.WriteLine($"MutableSegmentMaxItemCount: {MutableSegmentMaxItemCount}");
        Console.WriteLine($"EnableIncrementalBackup: {EnableIncrementalBackup}");
        Console.WriteLine($"EnableDiskSegmentCompression: {EnableDiskSegmentCompression}");
        Console.WriteLine($"WALCompressionBlockSize: {WALCompressionBlockSize}");
        Console.WriteLine($"DiskCompressionBlockSize: {DiskCompressionBlockSize}");
        Console.WriteLine($"DiskSegmentMaximumCachedBlockCount: {DiskSegmentMaximumCachedBlockCount}");
        Console.WriteLine($"MinimumSparseArrayLength: {MinimumSparseArrayLength}");
        Console.WriteLine($"EnableParalelInserts: {EnableParalelInserts}");
        Console.WriteLine($"DiskSegmentMode: {DiskSegmentMode}");
    }
}
