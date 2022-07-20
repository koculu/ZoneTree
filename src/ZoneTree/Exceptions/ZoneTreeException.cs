namespace Tenray.ZoneTree.Exceptions;

public class ZoneTreeException : Exception
{
    public ZoneTreeException(string message = null, Exception innerException = null) : base(message, innerException)
    {
    }
}
