using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Collections;

public sealed class FixedSizeMinHeap<TKey>
{
    readonly TKey[] keys;

    int count;

    readonly IRefComparer<TKey> comparer;

    public int Count => count;

    public int Size => keys.Length;

    public TKey MinValue
    {
        get
        {
            if (count == 0)
            {
                throw new InvalidOperationException("No elements in the heap.");
            }
            return keys[0];
        }
    }

    public FixedSizeMinHeap(int maximumSize, IRefComparer<TKey> comparer)
    {
        keys = new TKey[maximumSize];
        this.comparer = comparer;
    }

    public void Clear()
    {
        count = 0;
    }

    /// <summary>
    /// Inserts the new element, maintaining the heap property.
    /// 
    /// If the element is greater than the current min element, this function returns
    //     false without modifying the heap. Otherwise, it returns true.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>true if inserted</returns>
    public bool Insert(in TKey key)
    {
        if (count < keys.Length)
        {
            // There is room. We can add it and then Min-heapify.
            keys[count] = key;
            count++;
            HeapifyLastLeaf();
            return true;
        }
        else
        {
            // No more room. The element might not even fit in the heap. The check
            // is simple: if it's greater than the minimum element, then it can't be
            // inserted. Otherwise, we replace the head with it and reheapify.
            if (comparer.Compare(key, keys[0]) > 0)
            {
                keys[0] = key;
                HeapifyRoot();
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Replaces the minimum value in the heap with the user-provided value, and restores
    /// the heap property.
    /// </summary>
    /// <param name="newKey"></param>
    public void ReplaceMin(TKey newKey)
    {
        keys[0] = newKey;
        HeapifyRoot();
    }

    public void RemoveMin()
    {
        count--;

        if (count > 0)
        {
            keys[0] = keys[count];
            HeapifyRoot();
        }
    }

    void Swap(int i, int j)
    {
        (keys[j], keys[i]) = (keys[i], keys[j]);
    }

    void HeapifyRoot()
    {
        // We are heapifying from the head of the list.
        int i = 0;
        int n = count;

        while (i < n)
        {
            // Calculate the current child node indexes.
            int n0 = (i + 1) * 2 - 1;
            int n1 = n0 + 1;

            if (n0 < n && comparer.Compare(keys[i], keys[n0]) > 0)
            {
                // We have to select the bigger of the two subtrees, and float
                // the current element down. This maintains the Min-heap property.
                if (n1 < n && comparer.Compare(keys[n0], keys[n1]) > 0)
                {
                    Swap(i, n1);
                    i = n1;
                }
                else
                {
                    Swap(i, n0);
                    i = n0;
                }
            }
            else if (n1 < n && comparer.Compare(keys[i], keys[n1]) > 0)
            {
                // Float down the "right" subtree. We needn't compare this subtree
                // to the "left", because if the element was smaller than that, the
                // first if statement's predicate would have evaluated to true.
                Swap(i, n1);
                i = n1;
            }
            else
            {
                // Else, the current key is in its final position. Break out
                // of the current loop and return.
                break;
            }
        }
    }

    void HeapifyLastLeaf()
    {
        int i = count - 1;
        while (i > 0)
        {
            int j = (i + 1) / 2 - 1;

            if (comparer.Compare(keys[i], keys[j]) < 0)
            {
                Swap(i, j);
                i = j;
            }
            else
            {
                break;
            }
        }
    }

    public ReadOnlySpan<TKey> GetKeys()
    {
        return new ReadOnlySpan<TKey>(keys, 0, Count);
    }
}