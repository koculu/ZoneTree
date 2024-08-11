namespace Tenray.ZoneTree.Comparers;

public sealed class UInt32ComparerAscending : IRefComparer<uint>
{
    public int Compare(in uint x, in uint y)
    {
        return x.CompareTo(y);
    }
}
