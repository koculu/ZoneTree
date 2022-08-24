namespace Tenray.ZoneTree.Comparers;

public class StringCurrentCultureIgnoreCaseComparerAscending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.Compare(x, y, StringComparison.CurrentCultureIgnoreCase);
    }
}
