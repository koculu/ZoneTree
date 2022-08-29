using Humanizer;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Options;

namespace Playground.Benchmark;

public class ZoneTreeTestBase
{
    public virtual string RootPath { get; set; } = "../../data/";

    public virtual string DataPath => "base";

    public WriteAheadLogMode WALMode { get; set; } = WriteAheadLogMode.None;

    public int Count = 1_000_000;

    public CompressionMethod CompressionMethod { get; set; } = CompressionMethod.LZ4;

    protected string GetLabel(string label)
    {
        return $"{WALMode} {label}" +
            $" {Count.ToHuman()} - "
            + CompressionMethod;
    }

    protected ZoneTreeFactory<TKey, TValue> GetFactory<TKey, TValue>()
    {
        return new ZoneTreeFactory<TKey, TValue>()
            .SetMutableSegmentMaxItemCount(TestConfig.MutableSegmentMaxItemCount)
            .SetDiskSegmentCompression(TestConfig.EnableDiskSegmentCompression)
            .SetDiskSegmentCompressionBlockSize(TestConfig.DiskCompressionBlockSize)
            .SetDiskSegmentMaximumCachedBlockCount(TestConfig.DiskSegmentMaximumCachedBlockCount)
            .SetDataDirectory(DataPath)
            .ConfigureDiskSegmentOptions(x =>
            {
                x.DiskSegmentMode = TestConfig.DiskSegmentMode;
                x.CompressionMethod = CompressionMethod;
                x.CompressionLevel = TestConfig.CompressionLevel;
            })
            .ConfigureWriteAheadLogOptions(x =>
            {
                x.CompressionMethod = CompressionMethod;
                x.CompressionLevel = TestConfig.CompressionLevel;
                x.CompressionBlockSize = TestConfig.WALCompressionBlockSize;
                x.WriteAheadLogMode = WALMode;
                x.EnableIncrementalBackup = TestConfig.EnableIncrementalBackup;
            })
            .SetInitialSparseArrayLength(TestConfig.MinimumSparseArrayLength);
    }

    protected IZoneTree<TKey, TValue> OpenOrCreateZoneTree<TKey, TValue>()
    {
        return GetFactory<TKey, TValue>().OpenOrCreate();
    }

    protected ITransactionalZoneTree<TKey, TValue> OpenOrCreateTransactionalZoneTree<TKey, TValue>()
    {
        return GetFactory<TKey, TValue>().OpenOrCreateTransactional();
    }

    protected IMaintainer CreateMaintainer<TKey, TValue>(IZoneTree<TKey, TValue> zoneTree)
    {
        var maintainer = zoneTree.CreateMaintainer();
        maintainer.ThresholdForMergeOperationStart = TestConfig.ThresholdForMergeOperationStart;
        maintainer.MinimumSparseArrayLength = TestConfig.MinimumSparseArrayLength;
        return maintainer;
    }

    protected void AddOptions(IStatsCollector stats)
    {
        stats.SetOption("WAL", WALMode);
        stats.SetOption("Compression", CompressionMethod);
        stats.SetOption("Count", Count);
    }

    public void AddDatabaseFileUsage(IStatsCollector stats)
    {
        var path = DataPath;
        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
        long totalBytes = 0;
        foreach (var file in files)
        {
            var finfo = new FileInfo(file);            
            totalBytes += finfo.Length;
        }
        stats.AddAdditionalStats("Disk Usage", totalBytes.Bytes().Humanize());
    }
}
