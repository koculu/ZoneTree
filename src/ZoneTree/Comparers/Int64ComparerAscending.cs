using Tenray.Collections;

namespace Tenray;

public class Int64ComparerAscending : IRefComparer<long>
{
    public int Compare(in long x, in long y)
    {
        var r = x - y;
        if (r == 0)
            return 0;
        return r < 0 ? -1 : 1;
    }
}