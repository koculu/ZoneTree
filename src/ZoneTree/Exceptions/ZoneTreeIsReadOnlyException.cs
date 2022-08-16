namespace Tenray.ZoneTree.Exceptions;

public class ZoneTreeIsReadOnlyException : ZoneTreeException
{
    public ZoneTreeIsReadOnlyException()
        : base("Can't write to read-only ZoneTree.")
    {
    }
}