namespace Tenray.ZoneTree.Comparers;

public sealed class DoubleComparerAscending : IRefComparer<double>
{
    public int Compare(in double x, in double y)
    {
        return x.CompareTo(y);
    }
}