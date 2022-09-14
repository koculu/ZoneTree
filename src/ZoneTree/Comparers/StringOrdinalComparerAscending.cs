namespace Tenray.ZoneTree.Comparers;

public sealed class StringOrdinalComparerAscending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.CompareOrdinal(x, y);
    }
}
