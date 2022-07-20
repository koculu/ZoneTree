using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Comparers;

public class StringOrdinalComparerDescending : IRefComparer<string>
{
    public int Compare(in string x, in string y)
    {
        return string.CompareOrdinal(y, x);
    }
}