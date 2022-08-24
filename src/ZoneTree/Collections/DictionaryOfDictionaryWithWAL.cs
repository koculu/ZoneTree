using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Collections;

/// <summary>
/// Persistent Dictionary of dictionary implementation that is combined 
/// with a WriteAheadLog.
/// This class is not thread-safe.
/// </summary>
/// <typeparam name="TKey1"></typeparam>
/// <typeparam name="TKey2"></typeparam>
/// <typeparam name="TValue"></typeparam>
public sealed class DictionaryOfDictionaryWithWAL<TKey1, TKey2, TValue> : IDisposable
{
    readonly IncrementalIdProvider IdProvider = new();

    readonly long SegmentId;

    readonly string Category;

    readonly IWriteAheadLogProvider WriteAheadLogProvider;

    readonly IWriteAheadLog<TKey1, CombinedValue<TKey2, TValue>> WriteAheadLog;

    Dictionary<TKey1, IDictionary<TKey2, TValue>> Dictionary = new();

    public int Length => Dictionary.Count;

    public IReadOnlyList<TKey1> Keys => Dictionary.Keys.ToArray();

    public DictionaryOfDictionaryWithWAL(
        long segmentId,
        string category,
        IWriteAheadLogProvider writeAheadLogProvider,
        WriteAheadLogOptions options,
        ISerializer<TKey1> key1Serializer,
        ISerializer<TKey2> key2Serializer,
        ISerializer<TValue> valueSerializer)
    {
        WriteAheadLogProvider = writeAheadLogProvider;
        var combinedSerializer = new CombinedSerializer<TKey2, TValue>(key2Serializer, valueSerializer);
        WriteAheadLog = writeAheadLogProvider
            .GetOrCreateWAL(segmentId, category, options, key1Serializer, combinedSerializer);
        SegmentId = segmentId;
        Category = category;
        LoadFromWriteAheadLog();
    }

    void LoadFromWriteAheadLog()
    {
        var result = WriteAheadLog.ReadLogEntries(false, false, false);
        if (!result.Success)
        {
            if (result.HasFoundIncompleteTailRecord)
            {
                var incompleteTailException = result.IncompleteTailRecord;
                WriteAheadLog.TruncateIncompleteTailRecord(incompleteTailException);
            }
            else
            {
                WriteAheadLogProvider.RemoveWAL(SegmentId, Category);
                using var disposeWal = WriteAheadLog;
                throw new WriteAheadLogCorruptionException(SegmentId, result.Exceptions);
            }
        }

        var keys = result.Keys;
        var values = result.Values;
        var len = keys.Count;
        for (var i = 0; i < len; ++i)
        {
            Upsert(keys[i], values[i].Value1, values[i].Value2);
        }
    }

    public bool ContainsKey(in TKey1 key)
    {
        return Dictionary.ContainsKey(key);
    }

    public bool TryGetDictionary(in TKey1 key1, out IDictionary<TKey2, TValue> value)
    {
        return Dictionary.TryGetValue(key1, out value);
    }

    public bool TryGetValue(in TKey1 key1, in TKey2 key2, out TValue value)
    {
        if (Dictionary.TryGetValue(key1, out var dic))
            return dic.TryGetValue(key2, out value);
        value = default;
        return false;
    }

    public bool Upsert(in TKey1 key1, in TKey2 key2, in TValue value)
    {
        if (Dictionary.TryGetValue(key1, out var dic))
        {
            dic.Remove(key2);
            dic.Add(key2, value);
            WriteAheadLog.Append(key1, new CombinedValue<TKey2, TValue>(key2, value), NextOpIndex());
            return true;
        }
        dic = new Dictionary<TKey2, TValue>
        {
            { key2, value }
        };
        Dictionary[key1] = dic;
        WriteAheadLog.Append(key1, new CombinedValue<TKey2, TValue>(key2, value), NextOpIndex());
        return false;
    }

    long NextOpIndex()
    {
        return IdProvider.NextId();
    }

    public void Drop()
    {
        WriteAheadLogProvider.RemoveWAL(SegmentId, Category);
        WriteAheadLog?.Drop();
    }

    public void Dispose()
    {
        WriteAheadLogProvider.RemoveWAL(SegmentId, Category);
        WriteAheadLog?.Dispose();
    }

    public bool TryDeleteFromMemory(TKey1 key1)
    {
        return Dictionary.Remove(key1);
    }

    public void CompactWriteAheadLog()
    {
        var keys = Dictionary.Keys.ToArray();
        if (keys.Length == 0)
        {
            WriteAheadLog.ReplaceWriteAheadLog(
                keys,
                Array.Empty<CombinedValue<TKey2, TValue>>(),
                false);
            Dictionary = new();
            return;
        }
        var values = Dictionary.Values.ToArray();
        var manyKeys = values
            .SelectMany((x, i) =>
                Enumerable.Range(0, x.Count)
                .Select(y => keys[i])).ToArray();
        var manyValues = values
            .SelectMany(x => x.ToArray())
            .Select(x => new CombinedValue<TKey2, TValue>(x.Key, x.Value))
            .ToArray();
        WriteAheadLog.ReplaceWriteAheadLog(manyKeys, manyValues, false);
        var len = keys.Length;

        // recreate the dictionary to avoid empty space in the hash table.
        var newDictionary = new Dictionary<TKey1, IDictionary<TKey2, TValue>>((int)(len * 1.3));
        for (var i = 0; i < len; ++i)
        {
            newDictionary.Add(keys[i], values[i]);
        }
        Dictionary = newDictionary;
    }
}