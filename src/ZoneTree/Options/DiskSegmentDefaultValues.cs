namespace Tenray.ZoneTree.Options;

public static class DiskSegmentDefaultValues
{
    public static readonly DiskSegmentMode DiskSegmentMode = DiskSegmentMode.MultiPartDiskSegment;

    public static readonly int CompressionBlockSize = 4 * 1024 * 1024;

    public static readonly CompressionMethod CompressionMethod = CompressionMethod.LZ4;

    public static readonly int CompressionLevel = CompressionLevels.LZ4Fastest;

    public static readonly int MaximumRecordCount = 3_000_000;

    public static readonly int MinimumRecordCount = 1_500_000;

    public static readonly int KeyCacheSize = 1024;

    public static readonly int ValueCacheSize = 1024;

    public static readonly int KeyCacheRecordLifeTimeInMillisecond = 1024;

    public static readonly int ValueCacheRecordLifeTimeInMillisecond = 10_000;

    public static readonly int DefaultSparseArrayStepSize = 1024;
}
