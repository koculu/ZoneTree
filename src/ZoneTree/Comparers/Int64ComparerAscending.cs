namespace Tenray.ZoneTree.Comparers;

public sealed class Int64ComparerAscending : IRefComparer<long>
{
    public int Compare(in long x, in long y)
    {
        return x.CompareTo(y);
    }
}
