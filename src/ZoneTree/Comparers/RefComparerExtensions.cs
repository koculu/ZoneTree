using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Comparers;

public static class RefComparerExtensions
{
    public static IComparer<TKey> ToComparer<TKey>(this IRefComparer<TKey> refComparer)
    {
        return new ComparerFromRefComparer<TKey>(refComparer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreEqual<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreNotEqual<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ALessThanB<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) < 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ALessOrEqualToB<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) <= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AGreaterThanB<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AGreaterOrEqualToB<TKey>(
        this IRefComparer<TKey> refComparer,
        in TKey a,
        in TKey b)
    {
        return refComparer.Compare(a, b) >= 0;
    }
}