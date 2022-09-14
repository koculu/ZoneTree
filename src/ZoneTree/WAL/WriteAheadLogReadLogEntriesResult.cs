using Tenray.ZoneTree.Exceptions.WAL;

namespace Tenray.ZoneTree.WAL;

public sealed class WriteAheadLogReadLogEntriesResult<TKey, TValue>
{
    public bool Success { get; set; }

    public Dictionary<int, Exception> Exceptions = new();

    public IReadOnlyList<TKey> Keys { get; set; }

    public IReadOnlyList<TValue> Values { get; set; }

    public long MaximumOpIndex { get; set; }

    public bool HasFoundIncompleteTailRecord =>
        Exceptions.Count == 1 && Exceptions.Values.First() is IncompleteTailRecordFoundException;

    public IncompleteTailRecordFoundException IncompleteTailRecord =>
        HasFoundIncompleteTailRecord ?
        Exceptions.Values.First() as IncompleteTailRecordFoundException :
        null;
}
