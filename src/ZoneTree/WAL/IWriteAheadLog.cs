namespace Tenray.WAL;

public interface IWriteAheadLog<TKey, TValue> : IDisposable
{
    string FilePath { get; }

    void Append(in TKey key, in TValue value);

    void Drop();

    WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure);
}