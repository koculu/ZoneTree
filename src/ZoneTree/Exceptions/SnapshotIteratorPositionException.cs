namespace Tenray.ZoneTree.Exceptions;

public sealed class ZoneTreeIteratorPositionException : ZoneTreeException
{
    public ZoneTreeIteratorPositionException()
        : base($"ZoneTreeIterator is not pointing to a valid record. Have you forgotten Next() call?")
    {
    }
}