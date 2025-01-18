namespace Tenray.ZoneTree.Options;

/// <summary>
/// Represents the configuration options for disk segments in the ZoneTree.
/// </summary>
public sealed class DiskSegmentOptions
{
    /// <summary>
    /// Gets or sets the mode for the disk segment.
    /// Default value is <see cref="DiskSegmentMode.MultiPartDiskSegment"/>.
    /// </summary>
    public DiskSegmentMode DiskSegmentMode { get; set; }
        = DiskSegmentDefaultValues.DiskSegmentMode;

    /// <summary>
    /// Gets or sets the block size for disk segment compression, in bytes.
    /// Default value is 4 MB (4 * 1024 * 1024 bytes).
    /// </summary>
    public int CompressionBlockSize { get; set; } = DiskSegmentDefaultValues.CompressionBlockSize;

    /// <summary>
    /// Gets or sets the compression method used if compression is enabled.
    /// Default value is <see cref="CompressionMethod.LZ4"/>.
    /// </summary>
    public CompressionMethod CompressionMethod { get; set; } = DiskSegmentDefaultValues.CompressionMethod;

    /// <summary>
    /// Gets or sets the compression level for the selected compression method.
    /// Default value is <see cref="CompressionLevels.LZ4Fastest"/>.
    /// </summary>
    public int CompressionLevel { get; set; } = DiskSegmentDefaultValues.CompressionLevel;

    /// <summary>
    /// Gets or sets the maximum number of records allowed in a disk segment when 
    /// <see cref="DiskSegmentMode.MultiPartDiskSegment"/> is enabled.
    /// Default value is 3M records.
    /// </summary>
    public int MaximumRecordCount { get; set; } = DiskSegmentDefaultValues.MaximumRecordCount;

    /// <summary>
    /// Gets or sets the minimum number of records required in a disk segment when
    /// <see cref="DiskSegmentMode.MultiPartDiskSegment"/> is enabled, unless there
    /// are not enough records.
    /// Default value is 1.5M records.
    /// </summary>
    public int MinimumRecordCount { get; set; } = DiskSegmentDefaultValues.MinimumRecordCount;

    /// <summary>
    /// Gets or sets the size of the circular buffer cache for keys.
    /// This cache is checked before accessing the block cache during lookups and searches.
    /// Default value is 1024.
    /// </summary>
    public int KeyCacheSize { get; set; } = DiskSegmentDefaultValues.KeyCacheSize;

    /// <summary>
    /// Gets or sets the size of the circular buffer cache for values.
    /// This cache is checked before accessing the block cache during lookups and searches.
    /// Default value is 1024.
    /// </summary>
    public int ValueCacheSize { get; set; } = DiskSegmentDefaultValues.ValueCacheSize;

    /// <summary>
    /// Gets or sets the maximum lifetime of a record in the key cache, in milliseconds.
    /// Default value is 10,000 milliseconds (10 seconds).
    /// </summary>
    public int KeyCacheRecordLifeTimeInMillisecond { get; set; } = DiskSegmentDefaultValues.KeyCacheRecordLifeTimeInMillisecond;

    /// <summary>
    /// Gets or sets the maximum lifetime of a record in the value cache, in milliseconds.
    /// Default value is 10,000 milliseconds (10 seconds).
    /// </summary>
    public int ValueCacheRecordLifeTimeInMillisecond { get; set; } = DiskSegmentDefaultValues.ValueCacheRecordLifeTimeInMillisecond;

    /// <summary>
    /// Gets or sets the default step size for the default sparse array of disk segments.
    /// Setting the step size to zero disables loading and creating the default sparse array.
    /// Default value is 1024.
    /// </summary>
    public int DefaultSparseArrayStepSize { get; set; } = DiskSegmentDefaultValues.DefaultSparseArrayStepSize;
}
