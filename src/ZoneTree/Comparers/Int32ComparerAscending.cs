using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Comparers;

public class Int32ComparerAscending : IRefComparer<int>
{
    public int Compare(in int x, in int y)
    {
        return x - y;
    }
}
