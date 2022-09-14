namespace Tenray.ZoneTree.Exceptions;

public sealed class DatabaseAlreadyExistsException : ZoneTreeException
{
    public DatabaseAlreadyExistsException()
        : base($"ZoneTree database already exists. Try to open it instead of creating a new one.")
    {
    }
}
