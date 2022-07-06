namespace Tenray.WAL;

public class WriteAheadLogReadLogEntriesResult<TKey, TValue>
{
    public bool Success { get; set; }

    public Dictionary<int, Exception> Exceptions = new();
    
    public IReadOnlyList<TKey> Keys { get; set; }
    
    public IReadOnlyList<TValue> Values { get; set; }
}
