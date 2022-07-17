namespace Tenray.WAL;

public interface IWriteAheadLog<TKey, TValue> : IDisposable
{
    string FilePath { get; }

    void Append(in TKey key, in TValue value);

    void Drop();

    WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure);

    /// <summary>
    /// Replaces the entire write ahead log,
    /// with given keys and values.
    /// </summary>
    /// <param name="keys">new keys</param>
    /// <param name="values">new values</param>
    /// <returns>the difference: old file length - new file length.</returns>
    long ReplaceWriteAheadLog(TKey[] keys, TValue[] values);
}