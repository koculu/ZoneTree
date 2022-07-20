using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Comparers;

public class Int32ComparerDescending : IRefComparer<int>
{
    public int Compare(in int x, in int y)
    {
        return y - x;
    }
}
