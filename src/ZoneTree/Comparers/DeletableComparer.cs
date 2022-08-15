using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Comparers;

public class DeletableComparer<TValue> : IRefComparer<Deletable<TValue>>
{
    public IRefComparer<TValue> Comparer { get; }

    public DeletableComparer(IRefComparer<TValue> comparer)
    {
        Comparer = comparer;
    }

    public int Compare(in Deletable<TValue> x, in Deletable<TValue> y)
    {
        return Comparer.Compare(x.Value, y.Value);
    }
}

public static class DeletableComparer
{
    public static IRefComparer<Deletable<TValue>> From<TValue>(IRefComparer<TValue> comparer)
    {
        return new DeletableComparer<TValue>(comparer);
    }
}