namespace Tenray.ZoneTree.Comparers;

public class GuidComparerAscending : IRefComparer<Guid>
{
    public int Compare(in Guid x, in Guid y)
    {
        return x.CompareTo(y);
    }
}