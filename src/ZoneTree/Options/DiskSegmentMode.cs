namespace Tenray.ZoneTree.Options;

/// <summary>
/// Available Disk Segment Modes.
/// </summary>
public enum DiskSegmentMode : byte
{
    /// <summary>
    /// Disk segments are kept in single file.
    /// Recommended for db size < sizeof(int) x 10M
    /// </summary>
    SingleDiskSegment = 0,

    /// <summary>
    /// Disk Segments are partitioned into several files.
    /// Recommended for db size > sizeof(int) x 10M
    /// </summary>
    MultiPartDiskSegment = 1,
}
