namespace ZoneTree.Segments.Disk;

public sealed record DiskSegmentFile(
    long SegmentId,
    string Path,
    string FileName,
    long RecordCount,
    int Order = -1);
