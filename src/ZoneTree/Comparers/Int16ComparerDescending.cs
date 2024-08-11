namespace Tenray.ZoneTree.Comparers;

public sealed class Int16ComparerDescending : IRefComparer<short>
{
    public int Compare(in short x, in short y)
    {
        return y.CompareTo(x);
    }
}
