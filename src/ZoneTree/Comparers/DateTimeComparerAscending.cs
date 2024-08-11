namespace Tenray.ZoneTree.Comparers;

public sealed class DateTimeComparerAscending : IRefComparer<DateTime>
{
    public int Compare(in DateTime x, in DateTime y)
    {
        return x.CompareTo(y);
    }
}