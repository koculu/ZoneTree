namespace Tenray.ZoneTree.Comparers;

public class StringInvariantComparerAscending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.Compare(x, y, StringComparison.InvariantCulture);
    }
}
