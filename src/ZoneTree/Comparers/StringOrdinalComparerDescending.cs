namespace Tenray.ZoneTree.Comparers;

public sealed class StringOrdinalComparerDescending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.CompareOrdinal(y, x);
    }
}