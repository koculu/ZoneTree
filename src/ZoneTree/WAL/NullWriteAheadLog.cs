namespace Tenray.WAL;

public sealed class NullWriteAheadLog<TKey, TValue> : IWriteAheadLog<TKey, TValue>
{
    public string FilePath => null;

    public void Append(in TKey key, in TValue value)
    {
    }

    public WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure)
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
}
