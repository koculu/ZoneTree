namespace Tenray.ZoneTree.Comparers;

public sealed class Int32ComparerDescending : IRefComparer<int>
{
    public int Compare(in int x, in int y)
    {
        return y - x;
    }
}
