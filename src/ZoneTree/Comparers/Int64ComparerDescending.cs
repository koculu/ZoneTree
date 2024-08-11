namespace Tenray.ZoneTree.Comparers;

public sealed class Int64ComparerDescending : IRefComparer<long>
{
    public int Compare(in long x, in long y)
    {
        return y.CompareTo(x);
    }
}
