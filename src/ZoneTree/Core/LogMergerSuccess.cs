using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree.Core;

public sealed class LogMergerSuccess : LogObject
{
    public int DropCount { get; }

    public int SkipCount { get; }

    public long ElapsedMilliseconds { get; }

    public int TotalDropCount { get; }

    public int TotalSkipCount { get; }

    public LogMergerSuccess(
        int dropCount,
        int skipCount,
        long elapsedMilliseconds,
        int totalDropCount,
        int totalSkipCount)
    {
        DropCount = dropCount;
        SkipCount = skipCount;
        ElapsedMilliseconds = elapsedMilliseconds;
        TotalDropCount = totalDropCount;
        TotalSkipCount = totalSkipCount;
    }

    public override string ToString()
    {
        var total = TotalSkipCount + TotalDropCount;
        var dropPercentage = 1.0 * TotalDropCount / (total == 0 ? 1 : total);
        return
            $"Merge SUCCESS in {ElapsedMilliseconds} ms ({DropCount} / {SkipCount + DropCount})" +
            Environment.NewLine +
            $"Total Drop Ratio ({TotalDropCount} / {TotalSkipCount + TotalDropCount}) => {dropPercentage * 100:0.##}%";
    }
}