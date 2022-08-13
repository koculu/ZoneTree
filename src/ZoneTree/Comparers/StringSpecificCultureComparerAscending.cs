using System.Globalization;
using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Comparers;

public class StringSpecificCultureComparerAscending : IRefComparer<string>
{
    public readonly CultureInfo CultureInfo;

    public readonly bool IgnoreCase;

    public StringSpecificCultureComparerAscending(string culture, bool ignoreCase)
    {
        CultureInfo = CultureInfo.GetCultureInfo(culture);
        IgnoreCase = ignoreCase;
    }

    public int Compare(in string x, in string y)
    {
        return string.Compare(x, y, IgnoreCase, CultureInfo);
    }
}