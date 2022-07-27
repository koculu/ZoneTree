using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Exceptions;

public class WriteAheadLogModeMismatchException : ZoneTreeException
{
    public WriteAheadLogModeMismatchException(WriteAheadLogMode expected, WriteAheadLogMode given)
        : base($"Write ahead log mode does not match.\r\n expected: {expected}\r\n given: {given}")
    {
        Expected = expected;
        Given = given;
    }

    public WriteAheadLogMode Expected { get; }

    public WriteAheadLogMode Given { get; }
}