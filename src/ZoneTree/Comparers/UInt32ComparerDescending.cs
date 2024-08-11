namespace Tenray.ZoneTree.Comparers;

public sealed class UInt32ComparerDescending : IRefComparer<uint>
{
    public int Compare(in uint x, in uint y)
    {
        return y.CompareTo(x);
    }
}
