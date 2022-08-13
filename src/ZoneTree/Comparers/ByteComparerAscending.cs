using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Comparers;

public class ByteComparerAscending : IRefComparer<byte>
{
    public int Compare(in byte x, in byte y)
    {
        return x - y;
    }
}
