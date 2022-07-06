namespace Tenray;

public class WriteAheadLogCorruptionException : ZoneTreeException
{
    public Dictionary<int, Exception> Exceptions { get; }

    public WriteAheadLogCorruptionException(int segmentId, 
        Dictionary<int, Exception> exceptions)
        : base($"Write ahead log with segment id = {segmentId} is corrupted.")
    {
        Exceptions = exceptions;
    }
}
