using Tenray.ZoneTree.Collections.TimSort;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.WAL;

public static class WriteAheadLogUtility
{
    public static (IReadOnlyList<TKey> keys, IReadOnlyList<TValue> values)
        StableSortAndCleanUpDeletedAndDuplicatedKeys<TKey, TValue>(
            IReadOnlyList<TKey> keys,
            IReadOnlyList<TValue> values,
            IRefComparer<TKey> comparer,
            IsDeletedDelegate<TKey, TValue> isDeleted)
    {
        // WAL has unsorted data. Need to do following.
        // 1. stable sort keys and values based on keys
        // 2. discard deleted and duplicated items
        // 3. create new keys and values arrays.

        int len = keys.Count;
        var list = new KeyValuePocket<TKey, TValue>[len];
        var pocketComparer = new KeyValuePocketRefComparer<TKey, TValue>(comparer);
        for (var i = 0; i < len; ++i)
            list[len - i - 1] = new KeyValuePocket<TKey, TValue>(keys[i], values[i]);
        TimSort<KeyValuePocket<TKey, TValue>>.Sort(list, pocketComparer);

        var newKeys = new List<TKey>(len);
        var newValues = new List<TValue>(len);

        for (var i = 0; i < len; ++i)
        {
            var value = list[i].Value;
            var key = list[i].Key;
            if (isDeleted(key, value))
            {
                // discard deleted items;
                while (++i < len)
                {
                    if (comparer.Compare(key, list[i].Key) != 0)
                    {
                        --i;
                        break;
                    }
                }
                continue;
            }
            newKeys.Add(key);
            newValues.Add(value);

            // discard duplicated keys found in log entries.
            while (++i < len)
            {
                if (comparer.Compare(key, list[i].Key) != 0)
                {
                    --i;
                    break;
                }
            }
        }
        return (newKeys, newValues);
    }

    public static (IReadOnlyList<TKey> keys, IReadOnlyList<TValue> values)
        StableSortAndCleanUpDuplicatedKeys<TKey, TValue>(
            IReadOnlyList<TKey> keys,
            IReadOnlyList<TValue> values,
            IRefComparer<TKey> comparer)
    {
        // WAL has unsorted data. Need to do following.
        // 1. stable sort keys and values based on keys
        // 2. discard duplicated keys
        // 3. create new keys and values arrays.

        int len = keys.Count;
        var list = new KeyValuePocket<TKey, TValue>[len];
        var pocketComparer = new KeyValuePocketRefComparer<TKey, TValue>(comparer);
        for (var i = 0; i < len; ++i)
            list[len - i - 1] = new KeyValuePocket<TKey, TValue>(keys[i], values[i]);
        TimSort<KeyValuePocket<TKey, TValue>>.Sort(list, pocketComparer);

        var newKeys = new List<TKey>(len);
        var newValues = new List<TValue>(len);

        for (var i = 0; i < len; ++i)
        {
            var value = list[i].Value;
            var key = list[i].Key;
            newKeys.Add(key);
            newValues.Add(value);

            // discard duplicated keys found in log entries.
            while (++i < len)
            {
                if (comparer.Compare(key, list[i].Key) != 0)
                {
                    --i;
                    break;
                }
            }
        }
        return (newKeys, newValues);
    }
}
