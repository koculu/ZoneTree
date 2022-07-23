namespace Tenray.ZoneTree.Exceptions;

public class WriteAheadLogFullLogCorruptionException : ZoneTreeException
{
    public WriteAheadLogFullLogCorruptionException(string filePath)
        : base($"Write ahead log is corrupted. file: {filePath}.")
    {
    }
}
