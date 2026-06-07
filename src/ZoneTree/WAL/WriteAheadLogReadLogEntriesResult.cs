using ZoneTree.Exceptions.WAL;

namespace ZoneTree.WAL;

public sealed class WriteAheadLogReadLogEntriesResult<TKey, TValue>
{
    public bool Success { get; set; }

    public Dictionary<int, Exception> Exceptions = new();

    public IReadOnlyList<TKey> Keys { get; set; }

    public IReadOnlyList<TValue> Values { get; set; }

    /// <summary>
    /// The largest operation index found while reading the WAL.
    /// </summary>
    /// <remarks>
    /// This is used to recover the producer high-water mark after restart. It
    /// prevents later writes for the same key from receiving lower operation
    /// indexes than earlier writes already observed by replication or audit
    /// consumers. It is not a database-wide version.
    /// </remarks>
    public long MaximumOpIndex { get; set; }

    public bool HasFoundIncompleteTailRecord =>
        Exceptions.Count == 1 && Exceptions.Values.First() is IncompleteTailRecordFoundException;

    public IncompleteTailRecordFoundException IncompleteTailRecord =>
        HasFoundIncompleteTailRecord ?
        Exceptions.Values.First() as IncompleteTailRecordFoundException :
        null;
}
