namespace Tenray.ZoneTree.Exceptions;

/// <summary>
/// Internal exception to synchronize reads with disk segment drop operation.
/// </summary>
internal sealed class DiskSegmentIsDroppingException : ZoneTreeException
{
    public DiskSegmentIsDroppingException()
    {
    }
}