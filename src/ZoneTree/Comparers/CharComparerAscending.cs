namespace Tenray.ZoneTree.Comparers;

public class CharComparerAscending : IRefComparer<char>
{
    public int Compare(in char x, in char y)
    {
        return x - y;
    }
}
