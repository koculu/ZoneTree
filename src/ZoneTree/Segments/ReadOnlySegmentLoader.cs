using Tenray.Collections;
using Tenray.Segments;
using ZoneTree.Collections.TimSort;
using ZoneTree.Core;
using ZoneTree.WAL;

namespace Tenray;

public class ReadOnlySegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public ReadOnlySegmentLoader(        
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IReadOnlySegment<TKey, TValue> LoadReadOnlySegment(int segmentId)
    {
        var wal = Options.WriteAheadLogProvider.GetOrCreateWAL(segmentId);
        var result = wal.ReadLogEntries(false, false);
        
        if (!result.Success)
        {
            Options.WriteAheadLogProvider.RemoveWAL(segmentId);
            using var disposeWal = wal;
            throw new WriteAheadLogCorruptionException(segmentId, result.Exceptions);
        }
        // WAL has unsorted data. Need to do following.
        // 1. stable sort keys and values based on keys
        // 2. discard deleted items
        // 3. create new keys and values arrays.

        var len = result.Keys.Count;
        var keys = result.Keys;
        var values = result.Values;
        var list = new KeyValuePocket<TKey, TValue>[len];
        var comparer = Options.Comparer;
        var pocketComparer = new KeyValuePocketRefComparer<TKey, TValue>(comparer);
        for (var i = 0; i < len; ++i)
            list[i] = new KeyValuePocket<TKey, TValue>(keys[i], values[i]);
        TimSort<KeyValuePocket<TKey, TValue>>.Sort(list, pocketComparer);

        var newKeys = new List<TKey>(len);
        var newValues = new List<TValue>(len);

        var isValueDeleted = Options.IsValueDeleted;
        for (var i = 0; i < len; ++i)        
        {
            var value = list[i].Value;
            var key = list[i].Key;
            if (isValueDeleted(value))
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
        }

        return new ReadOnlySegment<TKey, TValue>(
            segmentId, 
            Options,
            newKeys, 
            newValues);
    }
}

public struct KeyValuePocket<TKey, TValue>
{
    public TKey Key;
    public TValue Value;

    public KeyValuePocket(TKey key, TValue value) : this()
    {
        Key = key;
        Value = value;
    }
}

public class KeyValuePocketRefComparer<TKey, TValue> : IRefComparer<KeyValuePocket<TKey, TValue>>
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

