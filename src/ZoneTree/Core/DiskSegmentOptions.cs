namespace Tenray.ZoneTree.Core;

/// <summary>
/// Represents the Disk Segment Options.
/// </summary>
public class DiskSegmentOptions
{
    /// <summary>
    /// Configures the disk segment mode.
    /// Default value is MultiPartDiskSegment.
    /// </summary>
    public DiskSegmentMode DiskSegmentMode { get; set; }
        = DiskSegmentMode.MultiPartDiskSegment;

    /// <summary>
    /// Configures the disk segment compression. Default is true.
    /// </summary>
    public bool EnableDiskSegmentCompression { get; set; } = true;

    /// <summary>
    /// The disk segment compression block size.
    /// Default: 10 MB
    /// </summary>
    public int DiskSegmentCompressionBlockSize { get; set; } = 1024 * 1024 * 10;

    /// <summary>
    /// The disk segment block cache limit.
    /// A disk segment cannot have more cache blocks than the limit.
    /// Total memory space that block cache can take is
    /// = DiskSegmentCompressionBlockSize X DiskSegmentBlockCacheLimit
    /// Default: 1024 * 1024 * 10 * 32 = 320 MB
    /// </summary>
    public int DiskSegmentBlockCacheLimit { get; set; } = 32;

    /// <summary>
    /// If MultiPartDiskSegment mode is enabled, it is the upper bound 
    /// record count of a disk segment.
    /// A disk segment cannot have record count more than this value.
    /// Default value is 3M.
    /// </summary>
    public int DiskSegmentMaximumRecordCount { get; set; } = 3_000_000;

    /// <summary>
    /// If MultiPartDiskSegment mode is enabled,
    /// the minimum record count cannot be lower than this value
    /// unless there isn't enough records.
    /// Default value is 1.5M.
    /// </summary>
    public int DiskSegmentMinimumRecordCount { get; set; } = 1_500_000;
}
