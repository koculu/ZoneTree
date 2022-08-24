using System.Diagnostics;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Collections.TimSort;

/// <summary>
/// TimSort static utility methods.
/// </summary>
internal static class TimSortUtils
{
    #region Public Static Methods

    /// <summary>
    /// Returns the minimum acceptable run length for an array of the specified
    /// length. Natural runs shorter than this will be extended with 
    /// BinarySort(K[], int, int, int).
    ///
    /// Roughly speaking, the computation is:
    ///
    ///  If n &lt; MIN_MERGE, return n (it's too small to bother with fancy stuff).
    ///  Else if n is an exact power of 2, return MIN_MERGE/2.
    ///  Else return an int k, MIN_MERGE/2 &lt;= k &lt;= MIN_MERGE, such that n/k
    ///   is close to, but strictly less than, an exact power of 2.
    ///
    /// For the rationale, see timsort.txt.
    /// </summary>
    /// <param name="n">The length of the array to be sorted.</param>
    /// <param name="minMerge">The minimum merge length.</param>
    /// <returns>The length of the minimum run to be merged.</returns>
    internal static int MinRunLength(int n, int minMerge)
    {
        Debug.Assert(n >= 0);

        int r = 0;  // Becomes 1 if any 1 bits are shifted off.
        while (n >= minMerge)
        {
            r |= n & 1;
            n >>= 1;
        }
        return n + r;
    }

    /// <summary>
    /// Locates the position at which to insert the specified key into the
    /// specified sorted range; if the range contains an element equal to key,
    /// returns the index of the leftmost equal element.
    /// </summary>
    /// <param name="key">The key whose insertion point to search for.</param>
    /// <param name="a">The array in which to search.</param>
    /// <param name="baseIdx">The index of the first element in the range.</param>
    /// <param name="len">The length of the range; must be &gt; 0</param>
    /// <param name="hint">Hint the index at which to begin the search, 0 &lt;= hint &lt; n.
    /// The closer hint is to the result, the faster this method will run.</param>
    internal static int GallopLeft<K>(K key, K[] a, int baseIdx, int len, int hint, IRefComparer<K> comparer)
    {
        Debug.Assert(len > 0 && hint >= 0 && hint < len);

        int lastOfs = 0;
        int ofs = 1;
        if (comparer.Compare(key, a[baseIdx + hint]) > 0)
        {
            // Gallop right until a[baseIdx+hint+lastOfs] < key <= a[baseIdx+hint+ofs]
            int maxOfs = len - hint;
            while (ofs < maxOfs && comparer.Compare(key, a[baseIdx + hint + ofs]) > 0)
            {
                lastOfs = ofs;
                ofs = (ofs << 1) + 1;
                if (ofs <= 0)
                { // int overflow.
                    ofs = maxOfs;
                }
            }
            if (ofs > maxOfs)
            {
                ofs = maxOfs;
            }

            // Make offsets relative to baseIdx.
            lastOfs += hint;
            ofs += hint;
        }
        else
        {
            // key <= a[baseIdx + hint]
            // Gallop left until a[baseIdx+hint-ofs] < key <= a[baseIdx+hint-lastOfs]
            int maxOfs = hint + 1;
            while (ofs < maxOfs && comparer.Compare(key, a[baseIdx + hint - ofs]) <= 0)
            {
                lastOfs = ofs;
                ofs = (ofs << 1) + 1;
                if (ofs <= 0)
                { // int overflow.
                    ofs = maxOfs;
                }
            }
            if (ofs > maxOfs)
            {
                ofs = maxOfs;
            }

            // Make offsets relative to baseIdx.
            int tmp = lastOfs;
            lastOfs = hint - ofs;
            ofs = hint - tmp;
        }
        Debug.Assert(-1 <= lastOfs && lastOfs < ofs && ofs <= len);

        // Now a[baseIdx+lastOfs] < key <= a[baseIdx+ofs], so key belongs somewhere
        // to the right of lastOfs but no farther right than ofs.  Do a binary
        // search, with invariant a[baseIdx + lastOfs - 1] < key <= a[baseIdx + ofs].
        lastOfs++;
        while (lastOfs < ofs)
        {
            int m = lastOfs + (ofs - lastOfs >> 1);

            if (comparer.Compare(key, a[baseIdx + m]) > 0)
                lastOfs = m + 1;    // a[baseIdx + m] < key
            else
                ofs = m;            // key <= a[baseIdx + m]
        }
        Debug.Assert(lastOfs == ofs);   // so a[baseIdx + ofs - 1] < key <= a[baseIdx + ofs]
        return ofs;
    }

    /// <summary>
    /// Like gallopLeft, except that if the range contains an element equal to
    /// key, gallopRight returns the index after the rightmost equal element.
    /// </summary>
    /// <param name="key">The key whose insertion point to search for.</param>
    /// <param name="a">The array in which to search.</param>
    /// <param name="baseIdx">The index of the first element in the range.</param>
    /// <param name="len">The length of the range; must be &gt; 0.</param>
    /// <param name="hint">The index at which to begin the search, 0 &lt;= hint &lt; n.
    /// The closer hint is to the result, the faster this method will run.</param>
    /// <returns>The int k,  0 &lt;= k &lt;= n such that a[b + k - 1] &lt;= key &lt; a[b + k]</returns>
    internal static int GallopRight<K>(K key, K[] a, int baseIdx, int len, int hint, IRefComparer<K> comparer)
    {
        Debug.Assert(len > 0 && hint >= 0 && hint < len);

        int ofs = 1;
        int lastOfs = 0;
        if (comparer.Compare(key, a[baseIdx + hint]) < 0)
        {
            // Gallop left until a[baseIdx + hint - ofs] <= key < a[baseIdx + hint - lastOfs]
            int maxOfs = hint + 1;
            while (ofs < maxOfs && comparer.Compare(key, a[baseIdx + hint - ofs]) < 0)
            {
                lastOfs = ofs;
                ofs = (ofs << 1) + 1;
                if (ofs <= 0)
                { // int overflow.
                    ofs = maxOfs;
                }
            }
            if (ofs > maxOfs)
            {
                ofs = maxOfs;
            }

            // Make offsets relative to baseIdx.
            int tmp = lastOfs;
            lastOfs = hint - ofs;
            ofs = hint - tmp;
        }
        else
        {
            // a[baseIdx + hint] <= key
            // Gallop right until a[baseIdx + hint + lastOfs] <= key < a[baseIdx + hint + ofs]
            int maxOfs = len - hint;
            while (ofs < maxOfs && comparer.Compare(key, a[baseIdx + hint + ofs]) >= 0)
            {
                lastOfs = ofs;
                ofs = (ofs << 1) + 1;
                if (ofs <= 0)
                { // int overflow.
                    ofs = maxOfs;
                }
            }
            if (ofs > maxOfs)
            {
                ofs = maxOfs;
            }

            // Make offsets relative to baseIdx.
            lastOfs += hint;
            ofs += hint;
        }
        Debug.Assert(-1 <= lastOfs && lastOfs < ofs && ofs <= len);

        // Now a[baseIdx + lastOfs] <= key < a[baseIdx + ofs], so key belongs somewhere to
        // the right of lastOfs but no farther right than ofs.  Do a binary
        // search, with invariant a[baseIdx + lastOfs - 1] <= key < a[baseIdx + ofs].
        lastOfs++;
        while (lastOfs < ofs)
        {
            int m = lastOfs + (ofs - lastOfs >> 1);

            if (comparer.Compare(key, a[baseIdx + m]) < 0)
                ofs = m;            // key < a[baseIdx + m]
            else
                lastOfs = m + 1;    // a[baseIdx + m] <= key
        }
        Debug.Assert(lastOfs == ofs);   // so a[baseIdx + ofs - 1] <= key < a[baseIdx + ofs]
        return ofs;
    }

    #endregion
}