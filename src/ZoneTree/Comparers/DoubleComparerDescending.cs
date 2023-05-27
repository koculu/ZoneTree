namespace Tenray.ZoneTree.Comparers;

public sealed class DoubleComparerDescending: IRefComparer<double>
{
    public int Compare(in double x, in double y)
    {
        var r = y - x;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}