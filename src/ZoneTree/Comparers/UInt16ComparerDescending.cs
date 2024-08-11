namespace Tenray.ZoneTree.Comparers;

public sealed class UInt16ComparerDescending : IRefComparer<ushort>
{
    public int Compare(in ushort x, in ushort y)
    {
        return y.CompareTo(x);
    }
}