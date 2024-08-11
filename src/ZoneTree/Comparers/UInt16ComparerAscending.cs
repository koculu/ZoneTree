namespace Tenray.ZoneTree.Comparers;

public sealed class UInt16ComparerAscending : IRefComparer<ushort>
{
    public int Compare(in ushort x, in ushort y)
    {
        return x.CompareTo(y);
    }
}