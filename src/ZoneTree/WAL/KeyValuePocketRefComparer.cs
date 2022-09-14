using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.WAL;

public sealed class KeyValuePocketRefComparer<TKey, TValue> : IRefComparer<KeyValuePocket<TKey, TValue>>
{
    public IRefComparer<TKey> Comparer { get; }

    public KeyValuePocketRefComparer(IRefComparer<TKey> comparer)
    {
        Comparer = comparer;
    }

    public int Compare(in KeyValuePocket<TKey, TValue> x, in KeyValuePocket<TKey, TValue> y)
    {
        return Comparer.Compare(x.Key, in y.Key);
    }
}

