namespace Tenray.ZoneTree.Comparers;

public sealed class CharComparerAscending : IRefComparer<char>
{
    public int Compare(in char x, in char y)
    {
        return x - y;
    }
}
