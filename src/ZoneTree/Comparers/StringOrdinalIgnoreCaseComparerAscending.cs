namespace Tenray.ZoneTree.Comparers;

public sealed class StringOrdinalIgnoreCaseComparerAscending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
