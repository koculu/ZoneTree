using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

public sealed class NullWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    public string FilePath => null;

    public bool EnableIncrementalBackup { get; set; }

    public void Append(in TKey key, in TValue value, long opIndex)
    {
    }

    public WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure,
        bool sortByOpIndexes)
    {
        return new WriteAheadLogReadLogEntriesResult<TKey, TValue>
        {
            Success = true,
            Keys = Array.Empty<TKey>(),
            Values = Array.Empty<TValue>(),
        };
    }

    public void Drop()
    {
    }

    public void Dispose()
    {
    }

    public long ReplaceWriteAheadLog(TKey[] keys, TValue[] values, bool disableBackup)
    {
        return 0;
    }

    public void MarkFrozen()
    {
    }

    public void TruncateIncompleteTailRecord(IncompleteTailRecordFoundException incompleteTailException)
    {
    }
}
