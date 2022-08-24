/*
 * Copyright (c) 2009, 2013, Oracle and/or its affiliates. All rights reserved.
 * Copyright 2009 Google Inc.  All Rights Reserved.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * This code is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General internal License version 2 only, as
 * published by the Free Software Foundation.  Oracle designates this
 * particular file as subject to the "Classpath" exception as provided
 * by Oracle in the LICENSE file that accompanied this code.
 *
 * This code is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General internal License
 * version 2 for more details (a copy is included in the LICENSE file that
 * accompanied this code).
 *
 * You should have received a copy of the GNU General internal License version
 * 2 along with this work; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301 USA.
 *
 * Please contact Oracle, 500 Oracle Parkway, Redwood Shores, CA 94065 USA
 * or visit www.oracle.com if you need additional information or have any
 * questions.
 */

/*
 * A stable, adaptive, iterative mergesort that requires far fewer than
 * n log(n) comparisons when running on partially sorted arrays, while
 * offering performance comparable to a traditional mergesort when run
 * on random arrays.  Like all proper mergesorts, this sort is stable and
 * runs O(n log n) time (worst case).  In the worst case, this sort requires
 * temporary storage space for n/2 object references; in the best case,
 * it requires only a small constant amount of space.
 *
 * This implementation was adapted from Tim Peters' list sort for
 * Python, which is described in detail here:
 *
 *   http://svn.python.org/projects/python/trunk/Objects/listsort.txt
 *
 * Tim's C code may be found here:
 *
 *   http://svn.python.org/projects/python/trunk/Objects/listobject.c
 *
 * The underlying techniques are described in this paper (and may have
 * even earlier origins):
 *
 *  "Optimistic Sorting and Information Theoretic Complexity"
 *  Peter McIlroy
 *  SODA (Fourth Annual ACM-SIAM Symposium on Discrete Algorithms),
 *  pp 467-474, Austin, Texas, 25-27 January 1993.
 *
 * While the API to this class consists solely of static methods, it is
 * (privately) instantiable; a TimSort instance holds the state of an ongoing
 * sort, assuming the input array is large enough to warrant the full-blown
 * TimSort. Small arrays are sorted in place, using a binary insertion sort.
 *
 * @author Josh Bloch
 */

/*
 * The below C# code is a port of the Java source code, with fixes applied
 * from:
 * 
 *   "OpenJDK’s java.utils.Collection.sort() is broken: The good, the bad and
 *   the worst case?" Stijn de Gouw1, Jurriaan Rot, Frank S. de Boer,
 *   Richard Bubel, Reiner Hähnle.
 * 
 *   http://envisage-project.eu/wp-content/uploads/2015/02/sorting.pdf
 *  
 * That paper proposes multiple possible fixes for a possible 
 * index-out-of-range exception. The below C# code uses the recommended fix
 * from the paper whereas the Java source applied one of the alternative 
 * fixes; possibly because it was a safer, more conservative approach for 
 * such a widely used implementation, i.e. the default sort algorithm for 
 * java collections.
 *
 * Colin Green, April 2018.
 */

// Note. Currently this will sort arrays of non-null elements only 
// (i.e. when handling arrays of reference types).

using System.Diagnostics;
using Tenray.ZoneTree.Comparers;
using static Tenray.ZoneTree.Collections.TimSort.TimSortUtils;

namespace Tenray.ZoneTree.Collections.TimSort;

/// <summary>
/// STABLE TimSort Implementation
/// </summary>
/// <typeparam name="T">The sort array element type.</typeparam>
internal sealed class TimSort<T>
{
    #region Consts

    /// <summary>
    /// This is the minimum sized sequence that will be merged. Shorter
    /// sequences will be lengthened by calling binarySort. If the entire
    /// array is less than this length, no merges will be performed.
    ///
    /// This constant should be a power of two. It was 64 in Tim Peters' C
    /// implementation, but 32 was empirically determined to work better in
    /// this implementation. In the unlikely event that you set this constant
    /// to be a number that's not a power of two, you'll need to change the
    /// {minRunLength} computation.
    ///
    /// If you decrease this constant, you must change the stackLen
    /// computation in the TimSort constructor, or you risk an
    /// ArrayOutOfBounds exception. See timsort.txt for a discussion
    /// of the minimum stack length required as a function of the length
    /// of the array being sorted and the minimum merge sequence length.
    /// </summary>
    const int MIN_MERGE = 32;

    /// <summary>
    /// When we get into galloping mode, we stay there until both runs win less
    /// often than MIN_GALLOP consecutive times.
    /// </summary>
    const int MIN_GALLOP = 7;

    /// <summary>
    /// Maximum initial size of tmp array, which is used for merging.  The array
    /// can grow to accommodate demand.
    ///
    /// Unlike Tim's original C version, we do not allocate this much storage
    /// when sorting smaller arrays. This change was required for performance.
    /// </summary>
    const int INITIAL_TMP_STORAGE_LENGTH = 256;

    #endregion

    #region Instance Fields

    private readonly IRefComparer<T> _comparer;

    // The array being sorted.
    private readonly T[] _a;

    // This controls when we get *into* galloping mode.  It is initialized
    // to MIN_GALLOP. The mergeLo and mergeHi methods nudge it higher for
    // random data, and lower for highly structured data.
    int _minGallop = MIN_GALLOP;

    // Temp storage for merges. A workspace array may optionally be
    // provided in constructor, and if so will be used as long as it
    // is big enough.
    T[] _tmp;

    // A stack of pending runs yet to be merged. Run i starts at
    // address base[i] and extends for len[i] elements.  It's always
    // true (so long as the indices are in bounds) that:
    //
    //     runBase[i] + runLen[i] == runBase[i + 1]
    //
    // so we could cut the storage for this, but it's a minor amount,
    // and keeping all the info explicit simplifies the code.
    int _stackSize = 0;  // Number of pending runs on stack
    readonly int[] _runBase;
    readonly int[] _runLen;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a TimSort instance to maintain the state of an ongoing sort.
    /// </summary>
    /// <param name="a">The array to be sorted.</param>
    /// <param name="work">An optional workspace array.</param>
    private TimSort(T[] a, T[] work, IRefComparer<T> comparer)
    {
        _comparer = comparer;
        _a = a;

        // Allocate temp storage (which may be increased later if necessary).
        int len = a.Length;
        int tlen = len < 2 * INITIAL_TMP_STORAGE_LENGTH ?
            len >> 1 : INITIAL_TMP_STORAGE_LENGTH;

        if (work is null || work.Length < tlen)
            _tmp = new T[tlen];
        else
            _tmp = work;

        // Allocate runs-to-be-merged stack (which cannot be expanded). The
        // stack length requirements are described in timsort.txt. The C
        // version always uses the same stack length (85), but this was
        // measured to be too expensive when sorting "mid-sized" arrays (e.g.,
        // 100 elements) in Java. Therefore, we use smaller (but sufficiently
        // large) stack lengths for smaller arrays.  The "magic numbers" in the
        // computation below must be changed if MIN_MERGE is decreased. See
        // the MIN_MERGE declaration above for more information.
        //
        // The stackLen constants defined here are taken from:
        // http://envisage-project.eu/wp-content/uploads/2015/02/sorting.pdf
        //
        // The values are lower than those used in the Java source that this source
        // is a port of. The reason is that the above linked paper identifies a
        // bug and two proposed fixes, one is to fix the invariant enforced by 
        // mergeCollapse, the other is to not apply that fix, but to increase stackLen
        // in line with the worst case scenarios without the invariant fix.
        //
        // The java source also seems to have an additional +1 safety margin 
        // (or off-by-one error?), that is not applied here in the spirit of
        // achieving maximum possible performance.
        //
        // Note that the python timsort uses a fixed stackLen of 85.
        int stackLen = len < 120 ? 4 :
                        len < 1542 ? 9 :
                        len < 119151 ? 18 : 39;

        _runBase = new int[stackLen];
        _runLen = new int[stackLen];
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Pushes the specified run onto the pending-run stack.
    /// </summary>
    /// <param name="runBase">Index of the first element in the run.</param>
    /// <param name="runLen">The number of elements in the run.</param>
    private void PushRun(int runBase, int runLen)
    {
        _runBase[_stackSize] = runBase;
        _runLen[_stackSize] = runLen;
        _stackSize++;
    }

    /// <summary>
    /// Examines the stack of runs waiting to be merged and merges adjacent runs
    /// until the stack invariants are re-established:
    ///
    ///     1. runLen[i - 3] > runLen[i - 2] + runLen[i - 1]
    ///     2. runLen[i - 2] > runLen[i - 1]
    ///
    /// This method is called each time a new run is pushed onto the stack,
    /// so the invariants are guaranteed to hold for i &lt; stackSize upon
    /// entry to the method.
    /// </summary>
    private void MergeCollapse()
    {
        // Note. Contains the fix from:
        // http://envisage-project.eu/proving-android-java-and-python-sorting-algorithm-is-broken-and-how-to-fix-it/
        // The Java version chose to address the bug by increasing 

        while (_stackSize > 1)
        {
            int n = _stackSize - 2;
            if (n >= 1 && _runLen[n - 1] <= _runLen[n] + _runLen[n + 1]
                 || n >= 2 && _runLen[n - 2] <= _runLen[n] + _runLen[n - 1])
            {
                if (_runLen[n - 1] < _runLen[n + 1])
                    n--;
            }
            else if (_runLen[n] > _runLen[n + 1])
            {
                break; // Invariant is established.

            }
            MergeAt(n);
        }
    }

    /// <summary>
    /// Merges all runs on the stack until only one remains. This method is
    /// called once, to complete the sort. 
    /// </summary>
    private void MergeForceCollapse()
    {
        while (_stackSize > 1)
        {
            int n = _stackSize - 2;
            if (n > 0 && _runLen[n - 1] < _runLen[n + 1])
            {
                n--;
            }

            MergeAt(n);
        }
    }

    /// <summary>
    /// Merges the two runs at stack indices i and i+1. Run i must be
    /// the penultimate or ante-penultimate run on the stack. In other words,
    /// i must be equal to stackSize-2 or stackSize-3.
    /// </summary>
    /// <param name="i">Stack index of the first of the two runs to merge.</param>
    private void MergeAt(int i)
    {
        Debug.Assert(_stackSize >= 2);
        Debug.Assert(i >= 0);
        Debug.Assert(i == _stackSize - 2 || i == _stackSize - 3);

        int base1 = _runBase[i];
        int len1 = _runLen[i];
        int base2 = _runBase[i + 1];
        int len2 = _runLen[i + 1];
        Debug.Assert(len1 > 0 && len2 > 0);
        Debug.Assert(base1 + len1 == base2);

        // Record the length of the combined runs; if i is the 3rd-last
        // run now, also slide over the last run (which isn't involved
        // in this merge).  The current run (i+1) goes away in any case.
        _runLen[i] = len1 + len2;
        if (i == _stackSize - 3)
        {
            _runBase[i + 1] = _runBase[i + 2];
            _runLen[i + 1] = _runLen[i + 2];
        }
        _stackSize--;

        // Find where the first element of run2 goes in run1. Prior elements
        // in run1 can be ignored (because they're already in place).
        int k = GallopRight(_a[base2], _a, base1, len1, 0, _comparer);
        Debug.Assert(k >= 0);
        base1 += k;
        len1 -= k;
        if (len1 == 0)
        {
            return;
        }

        // Find where the last element of run1 goes in run2. Subsequent elements.
        // in run2 can be ignored (because they're already in place).
        len2 = GallopLeft(_a[base1 + len1 - 1], _a, base2, len2, len2 - 1, _comparer);
        Debug.Assert(len2 >= 0);
        if (len2 == 0)
        {
            return;
        }

        // Merge remaining runs, using tmp array with min(len1, len2) elements.
        if (len1 <= len2)
            MergeLo(base1, len1, base2, len2);
        else
            MergeHi(base1, len1, base2, len2);
    }

    /// <summary>
    /// Merges two adjacent runs in place, in a stable fashion. The first
    /// element of the first run must be greater than the first element of the
    /// second run (a[base1] &gt; a[base2]), and the last element of the first run
    /// (a[base1 + len1-1]) must be greater than all elements of the second run.
    ///
    /// For performance, this method should be called only when len1 &lt;= len2;
    /// its twin, mergeHi should be called if len1 &gt;= len2. (Either method
    /// may be called if len1 == len2.)
    /// </summary>
    /// <param name="base1">Index of first element in first run to be merged.</param>
    /// <param name="len1">Length of first run to be merged (must be &gt; 0).</param>
    /// <param name="base2">Index of first element in second run to be merged (must be aBase + aLen).</param>
    /// <param name="len2">Index of first element in second run to be merged (must be aBase + aLen).</param>
    private void MergeLo(int base1, int len1, int base2, int len2)
    {
        Debug.Assert(len1 > 0 && len2 > 0 && base1 + len1 == base2);

        // Copy first run into temp array.
        T[] a = _a; // For performance
        T[] tmp = EnsureCapacity(len1);

        int cursor1 = 0;        // Indexes into tmp array.
        int cursor2 = base2;    // Indexes int a.
        int dest = base1;       // Indexes int a.
        Array.Copy(a, base1, tmp, cursor1, len1);

        // Move first element of second run and deal with degenerate cases.
        a[dest++] = a[cursor2++];
        if (--len2 == 0)
        {
            Array.Copy(tmp, cursor1, a, dest, len1);
            return;
        }

        if (len1 == 1)
        {
            Array.Copy(a, cursor2, a, dest, len2);
            a[dest + len2] = tmp[cursor1]; // Last element of run 1 to end of merge.
            return;
        }

        int minGallop = _minGallop;  // Use local variable for performance.
                                     //outer:
        while (true)
        {
            int count1 = 0; // Number of times in a row that first run won.
            int count2 = 0; // Number of times in a row that second run won.

            // Do the straightforward thing until (if ever) one run starts
            // winning consistently.
            do
            {
                Debug.Assert(len1 > 1 && len2 > 0);

                if (_comparer.Compare(a[cursor2], tmp[cursor1]) < 0)
                {
                    a[dest++] = a[cursor2++];
                    count2++;
                    count1 = 0;
                    if (--len2 == 0)
                    {
                        goto outerExit;
                    }
                }
                else
                {
                    a[dest++] = tmp[cursor1++];
                    count1++;
                    count2 = 0;
                    if (--len1 == 1)
                    {
                        goto outerExit;
                    }
                }
            }
            while ((count1 | count2) < minGallop);

            // One run is winning so consistently that galloping may be a
            // huge win. So try that, and continue galloping until (if ever)
            // neither run appears to be winning consistently anymore.
            do
            {
                Debug.Assert(len1 > 1 && len2 > 0);

                count1 = GallopRight(a[cursor2], tmp, cursor1, len1, 0, _comparer);
                if (count1 != 0)
                {
                    Array.Copy(tmp, cursor1, a, dest, count1);
                    dest += count1;
                    cursor1 += count1;
                    len1 -= count1;
                    if (len1 <= 1)
                    { // len1 == 1 || len1 == 0    
                        goto outerExit;
                    }
                }
                a[dest++] = a[cursor2++];
                if (--len2 == 0)
                {
                    goto outerExit;
                }

                count2 = GallopLeft(tmp[cursor1], a, cursor2, len2, 0, _comparer);
                if (count2 != 0)
                {
                    Array.Copy(a, cursor2, a, dest, count2);
                    dest += count2;
                    cursor2 += count2;
                    len2 -= count2;
                    if (len2 == 0)
                    {
                        goto outerExit;
                    }
                }
                a[dest++] = tmp[cursor1++];
                if (--len1 == 1)
                {
                    goto outerExit;
                }
                minGallop--;
            }
            while (count1 >= MIN_GALLOP | count2 >= MIN_GALLOP);

            if (minGallop < 0)
            {
                minGallop = 0;
            }

            // Note. Original source used +=1. The JDK version changed this to +=2 which in simple tests appears to be a better choice.
            minGallop += 2;  // Penalize for leaving gallop mode.
        }  // End of "outer" loop
    outerExit:

        _minGallop = minGallop < 1 ? 1 : minGallop;  // Write back to field.

        if (len1 == 1)
        {
            Debug.Assert(len2 > 0);
            Array.Copy(a, cursor2, a, dest, len2);
            a[dest + len2] = tmp[cursor1];  // Last element of run 1 to end of merge.
        }
        else if (len1 == 0)
        {
            throw new ArgumentException(
                "Comparison method violates its general contract!");
        }
        else
        {
            Debug.Assert(len2 == 0);
            Debug.Assert(len1 > 1);
            Array.Copy(tmp, cursor1, a, dest, len1);
        }
    }

    /// <summary>
    /// Like mergeLo, except that this method should be called only if
    /// len1 &gt;= len2; mergeLo should be called if len1 &lt;= len2.  (Either method
    /// may be called if len1 == len2.)
    /// </summary>
    /// <param name="base1">Index of first element in first run to be merged.</param>
    /// <param name="len1">Length of first run to be merged (must be &gt; 0).</param>
    /// <param name="base2">Index of first element in second run to be merged (must be aBase + aLen).</param>
    /// <param name="len2">Length of second run to be merged (must be &gt; 0).</param>
    private void MergeHi(int base1, int len1, int base2, int len2)
    {
        Debug.Assert(len1 > 0 && len2 > 0 && base1 + len1 == base2);

        // Copy second run into temp array.
        T[] a = _a; // For performance.
        T[] tmp = EnsureCapacity(len2);
        Array.Copy(a, base2, tmp, 0, len2);

        int cursor1 = base1 + len1 - 1; // Indexes into a.
        int cursor2 = len2 - 1;         // Indexes into tmp array.
        int dest = base2 + len2 - 1;    // Indexes into a.

        // Move last element of first run and deal with degenerate cases.
        a[dest--] = a[cursor1--];
        if (--len1 == 0)
        {
            Array.Copy(tmp, 0, a, dest - (len2 - 1), len2);
            return;
        }

        if (len2 == 1)
        {
            dest -= len1;
            cursor1 -= len1;
            Array.Copy(a, cursor1 + 1, a, dest + 1, len1);
            a[dest] = tmp[cursor2];
            return;
        }

        int minGallop = _minGallop;  // Use local variable for performance.

        while (true)
        {
            int count1 = 0; // Number of times in a row that first run won.
            int count2 = 0; // Number of times in a row that second run won.

            // Do the straightforward thing until (if ever) one run
            // appears to win consistently.
            do
            {
                Debug.Assert(len1 > 0 && len2 > 1);
                if (_comparer.Compare(tmp[cursor2], a[cursor1]) < 0)
                {
                    a[dest--] = a[cursor1--];
                    count1++;
                    count2 = 0;
                    if (--len1 == 0)
                    {
                        goto outerExit;
                    }
                }
                else
                {
                    a[dest--] = tmp[cursor2--];
                    count2++;
                    count1 = 0;
                    if (--len2 == 1)
                    {
                        goto outerExit;
                    }
                }
            }
            while ((count1 | count2) < minGallop);

            // One run is winning so consistently that galloping may be a
            // huge win. So try that, and continue galloping until (if ever)
            // neither run appears to be winning consistently anymore.
            do
            {
                Debug.Assert(len1 > 0 && len2 > 1);

                count1 = len1 - GallopRight(tmp[cursor2], a, base1, len1, len1 - 1, _comparer);
                if (count1 != 0)
                {
                    dest -= count1;
                    cursor1 -= count1;
                    len1 -= count1;
                    Array.Copy(a, cursor1 + 1, a, dest + 1, count1);
                    if (len1 == 0)
                    {
                        goto outerExit;
                    }
                }
                a[dest--] = tmp[cursor2--];
                if (--len2 == 1)
                {
                    goto outerExit;
                }

                count2 = len2 - GallopLeft(a[cursor1], tmp, 0, len2, len2 - 1, _comparer);
                if (count2 != 0)
                {
                    dest -= count2;
                    cursor2 -= count2;
                    len2 -= count2;
                    Array.Copy(tmp, cursor2 + 1, a, dest + 1, count2);
                    if (len2 <= 1)
                    { // len2 == 1 || len2 == 0
                        goto outerExit;
                    }
                }
                a[dest--] = a[cursor1--];
                if (--len1 == 0)
                {
                    goto outerExit;
                }
                minGallop--;
            }
            while (count1 >= MIN_GALLOP | count2 >= MIN_GALLOP);

            if (minGallop < 0)
            {
                minGallop = 0;
            }

            // Note. Original source used +=1. The JDK version changed this to +=2 which in simple tests appears to be a better choice.
            minGallop += 2;  // Penalize for leaving gallop mode.
        }  // End of "outer" loop
    outerExit:

        _minGallop = minGallop < 1 ? 1 : minGallop;  // Write back to field.

        if (len2 == 1)
        {
            Debug.Assert(len1 > 0);
            dest -= len1;
            cursor1 -= len1;
            Array.Copy(a, cursor1 + 1, a, dest + 1, len1);
            a[dest] = tmp[cursor2]; // Move first element of run2 to front of merge.
        }
        else if (len2 == 0)
        {
            throw new ArgumentException(
                "Comparison method violates its general contract!");
        }
        else
        {
            Debug.Assert(len1 == 0);
            Debug.Assert(len2 > 0);
            Array.Copy(tmp, 0, a, dest - (len2 - 1), len2);
        }
    }

    /// <summary>
    /// Ensures that the external array tmp has at least the specified
    /// number of elements, increasing its size if necessary. The size
    /// increases exponentially to ensure amortized linear time complexity.
    /// </summary>
    /// <param name="minCapacity">The minimum required capacity of the tmp array.</param>
    /// <returns>tmp, whether or not it grew.</returns>
    private T[] EnsureCapacity(int minCapacity)
    {
        if (_tmp.Length < minCapacity)
        {
            // Compute smallest power of 2 > minCapacity.
            int newSize = minCapacity;
            newSize |= newSize >> 1;
            newSize |= newSize >> 2;
            newSize |= newSize >> 4;
            newSize |= newSize >> 8;
            newSize |= newSize >> 16;
            newSize++;

            if (newSize < 0)
            { // Not bloody likely!
                newSize = minCapacity;
            }
            else
            {
                newSize = Math.Min(newSize, _a.Length >> 1);
            }

            T[] newArray = new T[newSize];
            _tmp = newArray;
        }
        return _tmp;
    }

    #endregion

    #region Private Static Methods

    /// <summary>
    /// Sorts the specified portion of the specified array using a binary
    /// insertion sort. This is the best method for sorting small numbers
    /// of elements. It requires O(n log n) compares, but O(n^2) data
    /// movement (worst case).
    ///
    /// If the initial part of the specified range is already sorted,
    /// this method can take advantage of it: the method assumes that the
    /// elements from index {lo}, inclusive, to {start},
    /// exclusive are already sorted.
    /// </summary>
    /// <param name="arr">The array in which a range is to be sorted.</param>
    /// <param name="lo">The index of the first element in the range to be sorted.</param>
    /// <param name="hi">the index after the last element in the range to be sorted.</param>
    /// <param name="start">he index of the first element in the range that is not already known to be sorted.</param>
    private static void BinarySort(T[] arr, int lo, int hi, int start, IRefComparer<T> comparer)
    {
        Debug.Assert(lo <= start && start <= hi);

        if (start == lo)
        {
            start++;
        }

        for (; start < hi; start++)
        {
            T pivot = arr[start];

            // Set left (and right) to the index where a[start] (pivot) belongs.
            int left = lo;
            int right = start;
            Debug.Assert(left <= right);

            // Invariants:
            //   pivot >= all in [lo, left).
            //   pivot <  all in [right, start).
            while (left < right)
            {
                int mid = left + right >> 1;
                if (comparer.Compare(pivot, arr[mid]) < 0)
                    right = mid;
                else
                    left = mid + 1;
            }
            Debug.Assert(left == right);

            // The invariants still hold: pivot >= all in [lo, left) and
            // pivot < all in [left, start), so pivot belongs at left.  Note
            // that if there are elements equal to pivot, left points to the
            // first slot after them -- that's why this sort is stable.
            // Slide elements over to make room for pivot.
            int n = start - left;  // The number of elements to move

            // Switch is just an optimization for Array.Copy in default case.
            switch (n)
            {
                case 2:
                    arr[left + 2] = arr[left + 1];
                    arr[left + 1] = arr[left];
                    break;
                case 1:
                    arr[left + 1] = arr[left];
                    break;
                default:
                    Array.Copy(arr, left, arr, left + 1, n);
                    break;
            }
            arr[left] = pivot;
        }
    }

    /// <summary>
    /// Returns the length of the run beginning at the specified position in
    /// the specified array and reverses the run if it is descending (ensuring
    /// that the run will always be ascending when the method returns).
    ///
    /// A run is the longest ascending sequence with:
    ///
    ///    a[lo] &lt;= a[lo + 1] &lt;= a[lo + 2] &lt;= ...
    ///
    /// or the longest descending sequence with:
    ///
    ///    a[lo] &gt;  a[lo + 1] &gt;  a[lo + 2] &gt;  ...
    ///
    /// For its intended use in a stable mergesort, the strictness of the
    /// definition of "descending" is needed so that the call can safely
    /// reverse a descending sequence without violating stability.
    /// </summary>
    /// <param name="a">The array in which a run is to be counted and possibly reversed.</param>
    /// <param name="lo">Index of the first element in the run.</param>
    /// <param name="hi">index after the last element that may be contained in the run. It is required that <code>lo &lt; hi</code>.</param>
    /// <returns>The length of the run beginning at the specified position in the specified array.</returns>
    private static int CountRunAndMakeAscending(T[] a, int lo, int hi, IRefComparer<T> comparer)
    {
        Debug.Assert(lo < hi);

        int runHi = lo + 1;
        if (runHi == hi)
        {
            return 1;
        }

        // Find end of run, and reverse range if descending.
        if (comparer.Compare(a[runHi++], a[lo]) < 0)
        {
            // Descending.
            while (runHi < hi && comparer.Compare(a[runHi], a[runHi - 1]) < 0)
            {
                runHi++;
            }
            ReverseRange(a, lo, runHi);
        }
        else
        {   // Ascending.
            while (runHi < hi && comparer.Compare(a[runHi], a[runHi - 1]) >= 0)
            {
                runHi++;
            }
        }

        return runHi - lo;
    }

    /// <summary>
    /// Reverse the specified range of the specified array.
    /// </summary>
    /// <param name="a">The array in which a range is to be reversed.</param>
    /// <param name="lo">The index of the first element in the range to be reversed.</param>
    /// <param name="hi">The index after the last element in the range to be reversed.</param>
    internal static void ReverseRange(T[] a, int lo, int hi)
    {
        hi--;
        while (lo < hi)
        {
            T t = a[lo];
            a[lo++] = a[hi];
            a[hi--] = t;
        }
    }

    #endregion

    #region internal Static Methods [Sort API]

    /// <summary>
    /// Sorts the given array.
    /// </summary>
    /// <param name="arr">The array to be sorted.</param>
    internal static void Sort(T[] arr, IRefComparer<T> comparer)
    {
        Sort(arr, 0, arr.Length, null, comparer);
    }

    /// <summary>
    /// Sorts the specified range within the given array.
    /// </summary>
    /// <param name="arr">The array to be sorted.</param>
    /// <param name="index">The starting index of the range to sort.</param>
    /// <param name="length">The number of elements in the range to sort.</param>
    internal static void Sort(T[] arr, int index, int length, IRefComparer<T> comparer)
    {
        Sort(arr, index, length, null, comparer);
    }

    /// <summary>
    /// Sorts the specified range within the given array, using the given workspace array slice
    /// for temp storage when possible.
    /// </summary>
    /// <param name="arr">The array to be sorted.</param>
    /// <param name="index">The starting index of the range to sort.</param>
    /// <param name="length">The number of elements in the range to sort.</param>
    /// <param name="work">An optional workspace array.</param>
    internal static void Sort(T[] arr, int index, int length, T[] work, IRefComparer<T> comparer)
    {
        Debug.Assert(arr is not null && index >= 0 && length >= 0 && index + length <= arr.Length);

        if (length < 2)
        {
            return; // Arrays of size 0 and 1 are always sorted.
        }

        // If array is small, do a "mini-TimSort" with no merges.
        int lo = index;
        int hi = index + length;

        if (length < MIN_MERGE)
        {
            int initRunLen = CountRunAndMakeAscending(arr, lo, hi, comparer);
            BinarySort(arr, lo, hi, lo + initRunLen, comparer);
            return;
        }

        // March over the array once, left to right, finding natural runs,
        // extending short natural runs to minRun elements, and merging runs
        // to maintain stack invariant.
        var ts = new TimSort<T>(arr, work, comparer);
        int minRun = MinRunLength(length, MIN_MERGE);
        int nRemaining = length;
        do
        {
            // Identify next run.
            int runLen = CountRunAndMakeAscending(arr, lo, hi, comparer);

            // If run is short, extend to min(minRun, nRemaining).
            if (runLen < minRun)
            {
                int force = nRemaining <= minRun ? nRemaining : minRun;
                BinarySort(arr, lo, lo + force, lo + runLen, comparer);
                runLen = force;
            }

            // Push run onto pending-run stack, and maybe merge.
            ts.PushRun(lo, runLen);
            ts.MergeCollapse();

            // Advance to find next run.
            lo += runLen;
            nRemaining -= runLen;
        }
        while (nRemaining != 0);

        // Merge all remaining runs to complete sort.
        Debug.Assert(lo == hi);
        ts.MergeForceCollapse();
        Debug.Assert(ts._stackSize == 1);
    }

    #endregion
}