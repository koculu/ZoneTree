namespace Tenray.ZoneTree.Exceptions;

public sealed class WriteAheadLogFullLogCorruptionException : ZoneTreeException
{
    public WriteAheadLogFullLogCorruptionException(string filePath)
        : base($"Write ahead log is corrupted. file: {filePath}.")
    {
    }
}
