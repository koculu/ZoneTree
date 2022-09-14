using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Exceptions;

public sealed class CompressionLevelIsOutOfRangeException : ZoneTreeException
{
    public CompressionLevelIsOutOfRangeException(
        string option,
        CompressionMethod method,
        int level)
        : base($"Selected compression level ({level}) for ({option}) is not valid for the selected compression method ({method}).")
    {
    }
}