namespace Tenray.ZoneTree.Exceptions;

public sealed class BTreeIsReadOnlyException : ZoneTreeException
{
    public BTreeIsReadOnlyException()
        : base("Can't write to read-only BTree.")
    {
    }
}