namespace Tenray.ZoneTree.Comparers;

public sealed class UInt64ComparerDescending : IRefComparer<ulong>
{
    public int Compare(in ulong x, in ulong y)
    {
        return y.CompareTo(x);
    }
}