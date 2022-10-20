namespace Tenray.ZoneTree.Exceptions;

public sealed class WriteAheadLogCorruptionException : ZoneTreeException
{
    public Dictionary<int, Exception> Exceptions { get; }

    public WriteAheadLogCorruptionException(long segmentId,
        Dictionary<int, Exception> exceptions)
        : base($"Write ahead log with segment id = {segmentId} is corrupted.", 
            exceptions == null ? null : new AggregateException(exceptions.Values))
    {
        Exceptions = exceptions;
    }
}
