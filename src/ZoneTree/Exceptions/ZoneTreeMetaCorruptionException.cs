namespace Tenray.ZoneTree.Exceptions;

public class ZoneTreeMetaCorruptionException : ZoneTreeException
{
    public ZoneTreeMetaCorruptionException()
        : base($"Tree meta data is corrupted. Recovery is required.")
    {
    }
}
