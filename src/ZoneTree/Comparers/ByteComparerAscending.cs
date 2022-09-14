namespace Tenray.ZoneTree.Comparers;

public sealed class ByteComparerAscending : IRefComparer<byte>
{
    public int Compare(in byte x, in byte y)
    {
        return x - y;
    }
}
