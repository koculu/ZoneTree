namespace Tenray.ZoneTree;

/// <summary>
/// Available Disk Segment Modes.
/// </summary>
public enum DiskSegmentMode
{
    /// <summary>
    /// Disk segments are kept in single file.
    /// Recommended for db size < sizeof(int) x 10M
    /// </summary>
    SingleDiskSegment,

    /// <summary>
    /// Disk Segments are partitioned into several files.
    /// Recommended for db size > sizeof(int) x 10M
    /// </summary>
    MultiPartDiskSegment,
}