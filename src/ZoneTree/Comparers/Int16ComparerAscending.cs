namespace Tenray.ZoneTree.Comparers;

public sealed class Int16ComparerAscending : IRefComparer<short>
{
    public int Compare(in short x, in short y)
    {
        return x - y;
    }
}
