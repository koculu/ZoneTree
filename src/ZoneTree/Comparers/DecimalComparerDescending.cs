namespace Tenray.ZoneTree.Comparers;

public sealed class DecimalComparerDescending : IRefComparer<decimal>
{
    public int Compare(in decimal x, in decimal y)
    {
        return y.CompareTo(x);
    }
}