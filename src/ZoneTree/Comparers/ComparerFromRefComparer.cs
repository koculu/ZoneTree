using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.Comparers;

public sealed class ComparerFromRefComparer<TKey> : IComparer<TKey>
{
    public readonly IRefComparer<TKey> RefComparer;

    public ComparerFromRefComparer(IRefComparer<TKey> refComparer)
    {
        RefComparer = refComparer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(TKey x, TKey y)
    {
        return RefComparer.Compare(x, y);
    }
}
