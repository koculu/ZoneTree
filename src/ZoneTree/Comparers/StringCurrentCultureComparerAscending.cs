namespace Tenray.ZoneTree.Comparers;

public sealed class StringCurrentCultureComparerAscending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.Compare(x, y, StringComparison.CurrentCulture);
    }
}
