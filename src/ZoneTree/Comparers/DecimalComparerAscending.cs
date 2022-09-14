namespace Tenray.ZoneTree.Comparers;

public sealed class DecimalComparerAscending : IRefComparer<decimal>
{
    public int Compare(in decimal x, in decimal y)
    {
        var r = x - y;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}
