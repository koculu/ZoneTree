using Tenray.Collections;

namespace Tenray;

public class IntegerComparerDescending : IRefComparer<int>
{
    public int Compare(in int x, in int y)
    {
        return y - x;
    }
}
