using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

public interface IWriteAheadLog<TKey, TValue> : IDisposable
{
    string FilePath { get; }

    bool EnableIncrementalBackup { get; set; }

    void Append(in TKey key, in TValue value, long opIndex);

    void Drop();

    WriteAheadLogReadLogEntriesResult<TKey, TValue> ReadLogEntries(
        bool stopReadOnException,
        bool stopReadOnChecksumFailure,
        bool sortByOpIndexes);

    /// <summary>
    /// Replaces the entire write ahead log,
    /// with given keys and values.
    /// If enabled, appends current wal data to the incremental backup log.
    /// </summary>
    /// <param name="keys">new keys</param>
    /// <param name="values">new values</param>
    /// <param name="disableBackup">disable backup regardless of wal flag.</param>
    /// <returns>the difference: old file length - new file length.</returns>
    long ReplaceWriteAheadLog(TKey[] keys, TValue[] values, bool disableBackup);
    
    /// <summary>
    /// Informs the write ahead log that no further writes will be sent.
    /// to let WAL optimize itself.
    /// </summary>
    void MarkFrozen();
    
    /// <summary>
    /// Truncates incomplete tail record.
    /// </summary>
    /// <param name="incompleteTailException"></param>
    void TruncateIncompleteTailRecord(IncompleteTailRecordFoundException incompleteTailException);
}