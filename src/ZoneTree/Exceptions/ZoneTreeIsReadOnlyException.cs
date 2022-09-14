namespace Tenray.ZoneTree.Exceptions;

public sealed class ZoneTreeIsReadOnlyException : ZoneTreeException
{
    public ZoneTreeIsReadOnlyException()
        : base("Can't write to read-only ZoneTree.")
    {
    }
}