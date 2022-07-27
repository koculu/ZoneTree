namespace Tenray.ZoneTree.Exceptions;

public class DiskSegmentCompressionModeMismatchException : ZoneTreeException
{
    public DiskSegmentCompressionModeMismatchException(bool expected, bool given)
        : base($"Disk segment compression mode does not match.\r\n expected: {expected}\r\n given: {given}")
    {
        Expected = expected;
        Given = given;
    }

    public bool Expected { get; }

    public bool Given { get; }
}