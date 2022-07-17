using Tenray.Collections;

namespace Tenray;

public class Int32ComparerAscending : IRefComparer<int>
{
    public int Compare(in int x, in int y)
    {
        return x - y;
    }
}
