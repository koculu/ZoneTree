namespace Tenray.ZoneTree.Exceptions;

public class DataIsTooBigToLoadAtOnce : ZoneTreeException
{
    public DataIsTooBigToLoadAtOnce(long dataLength, long maximumAllowedLength)
        : base($"Data is too big to load at once. {dataLength} > {maximumAllowedLength}")
    {
        DataLength = dataLength;
        MaximumAllowedLength = maximumAllowedLength;
    }

    public long DataLength { get; }
    public long MaximumAllowedLength { get; }
}
