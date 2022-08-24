using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Collections;

/// <summary>
/// Persistent Dictionary implementation that is combined 
/// with a WriteAheadLog.
/// This class is not thread-safe.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public sealed class DictionaryWithWAL<TKey, TValue> : IDisposable
{
    readonly IncrementalIdProvider IdProvider = new();

    readonly long SegmentId;

    readonly string Category;

    readonly IWriteAheadLogProvider WriteAheadLogProvider;

    readonly IWriteAheadLog<TKey, TValue> WriteAheadLog;

    readonly IRefComparer<TKey> Comparer;

    readonly IsValueDeletedDelegate<TValue> IsValueDeleted;

    readonly MarkValueDeletedDelegate<TValue> MarkValueDeleted;

    Dictionary<TKey, TValue> Dictionary = new();

    public int Length => Dictionary.Count;

    volatile int _logLength = 0;

    public int LogLength => _logLength;

    public TKey[] Keys => Dictionary.Keys.ToArray();

    public IReadOnlyList<TValue> Values => Dictionary.Values.ToArray();

    public IEnumerable<KeyValuePair<TKey, TValue>> Enumerable => Dictionary.AsEnumerable();

    public DictionaryWithWAL(
        long segmentId,
        string category,
        IWriteAheadLogProvider writeAheadLogProvider, 
        WriteAheadLogOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        IRefComparer<TKey> comparer,
        IsValueDeletedDelegate<TValue> isValueDeleted,
        MarkValueDeletedDelegate<TValue> markValueDeleted)
    {
        WriteAheadLogProvider = writeAheadLogProvider;
        Comparer = comparer;
        WriteAheadLog = writeAheadLogProvider
            .GetOrCreateWAL(segmentId, category, options, keySerializer, valueSerializer);
        SegmentId = segmentId;
        Category = category;
        IsValueDeleted = isValueDeleted;
        MarkValueDeleted = markValueDeleted;
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

        (var newKeys, var newValues) = WriteAheadLogUtility
            .StableSortAndCleanUpDeletedKeys(
            result.Keys,
            result.Values,
            Comparer,
            IsValueDeleted);
        var len = newKeys.Count;
        for (var i = 0; i < len; ++i)
        {
            Dictionary.Remove(newKeys[i]);
            Dictionary.Add(newKeys[i], newValues[i]);
        }
        _logLength = Dictionary.Count;
    }

    public bool ContainsKey(in TKey key)
    {
        return Dictionary.ContainsKey(key);
    }

    public bool TryGetValue(in TKey key, out TValue value)
    {
        return Dictionary.TryGetValue(key, out value);
    }

    public bool Upsert(in TKey key, in TValue value)
    {
        ++_logLength;
        if (Dictionary.ContainsKey(key))
        {
            Dictionary[key] = value;
            WriteAheadLog.Append(key, value, NextOpIndex());
            return false;
        }
        Dictionary.Add(key, value);
        WriteAheadLog.Append(key, value, NextOpIndex());
        return true;
    }

    public bool TryDeleteFromMemory(in TKey key)
    {
        return Dictionary.Remove(key);
    }

    public bool TryDelete(in TKey key)
    {
        Dictionary.TryGetValue(key, out var value);
        MarkValueDeleted(ref value);
        var result = Dictionary.Remove(key);
        WriteAheadLog.Append(key, value, NextOpIndex());
        return result;
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

    public void CompactWriteAheadLog()
    {
        var keys = Dictionary.Keys.ToArray();
        var values = Dictionary.Values.ToArray();
        WriteAheadLog.ReplaceWriteAheadLog(keys, values, false);

        var len = keys.Length;
        // recreate the dictionary to avoid empty space in the hash table.
        var newDictionary = new Dictionary<TKey, TValue>((int)(len * 1.3));
        for (var i = 0; i < len; ++i)
        {
            newDictionary.Add(keys[i], values[i]);
        }
        Dictionary = newDictionary;
        _logLength = newDictionary.Count;
    }
}