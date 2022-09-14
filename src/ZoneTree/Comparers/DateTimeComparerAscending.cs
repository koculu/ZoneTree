namespace Tenray.ZoneTree.Comparers;

public sealed class DateTimeComparerAscending : IRefComparer<DateTime>
{
    public int Compare(in DateTime x, in DateTime y)
    {
        var r = x.Ticks - y.Ticks;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}
