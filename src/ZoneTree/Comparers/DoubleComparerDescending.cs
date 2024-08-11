namespace Tenray.ZoneTree.Comparers;

public sealed class DoubleComparerDescending : IRefComparer<double>
{
    public int Compare(in double x, in double y)
    {
        return y.CompareTo(x);
    }
}