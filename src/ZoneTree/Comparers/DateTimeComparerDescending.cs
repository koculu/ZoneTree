namespace Tenray.ZoneTree.Comparers;

public sealed class DateTimeComparerDescending : IRefComparer<DateTime>
{
    public int Compare(in DateTime x, in DateTime y)
    {
        return y.CompareTo(x);
    }
}