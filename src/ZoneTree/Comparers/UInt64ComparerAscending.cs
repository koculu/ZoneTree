namespace Tenray.ZoneTree.Comparers;

public sealed class UInt64ComparerAscending : IRefComparer<ulong>
{
    public int Compare(in ulong x, in ulong y)
    {
        return x.CompareTo(y);
    }
}