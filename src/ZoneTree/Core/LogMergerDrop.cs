using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree.Core;

public sealed class LogMergerDrop : LogObject
{
    public long SegmentId { get; }

    public int DropCount { get; }

    public int SkipCount { get; }

    public LogMergerDrop(long segmentId, int dropCount, int skipCount)
    {
        SegmentId = segmentId;
        DropCount = dropCount;
        SkipCount = skipCount;
    }

    public override string ToString()
    {
        return $"drop: {SegmentId} ({DropCount} / {SkipCount + DropCount})";
    }
}
