namespace Tenray.ZoneTree.Exceptions;

public sealed class ZoneTreeMetaCorruptionException : ZoneTreeException
{
    public ZoneTreeMetaCorruptionException()
        : base($"Tree meta data is corrupted. Recovery is required.")
    {
    }
}
