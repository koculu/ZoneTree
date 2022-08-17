namespace Tenray.ZoneTree.Exceptions;

public class DiskSegmentCompressionBlockSizeMismatchException : ZoneTreeException
{
    public DiskSegmentCompressionBlockSizeMismatchException(int expected, int given)
        : base($"Disk segment compression block size does not match.\r\n expected: {expected}\r\n given: {given}")
    {
        Expected = expected;
        Given = given;
    }

    public int Expected { get; }

    public int Given { get; }
}
