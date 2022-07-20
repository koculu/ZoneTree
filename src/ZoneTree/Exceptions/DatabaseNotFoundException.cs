namespace Tenray.ZoneTree.Exceptions;

public class DatabaseNotFoundException : ZoneTreeException
{
    public DatabaseNotFoundException()
        : base($"ZoneTree database is not found. You may create a new one by calling Create / OpenCreate methods.")
    {
    }
}