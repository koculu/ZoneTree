namespace Tenray.ZoneTree.Exceptions;

public sealed class InvalidMergeRangeException : ZoneTreeException
{
    public InvalidMergeRangeException(int from, int to)
        : base($"Invalid merge range exception. from: {from} to: {to}")
    {
    }
}