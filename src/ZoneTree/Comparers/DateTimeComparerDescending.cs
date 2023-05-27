namespace Tenray.ZoneTree.Comparers;

public sealed class DateTimeComparerDescending : IRefComparer<DateTime>
{
    public int Compare(in DateTime x, in DateTime y)
    {
        var r = y.Ticks - x.Ticks;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}