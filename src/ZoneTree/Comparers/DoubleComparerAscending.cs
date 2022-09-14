namespace Tenray.ZoneTree.Comparers;

public sealed class DoubleComparerAscending : IRefComparer<double>
{
    public int Compare(in double x, in double y)
    {
        var r = x - y;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}
