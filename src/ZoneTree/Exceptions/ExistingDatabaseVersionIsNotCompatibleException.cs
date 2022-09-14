namespace Tenray.ZoneTree.Exceptions;

public sealed class ExistingDatabaseVersionIsNotCompatibleException : ZoneTreeException
{
    public ExistingDatabaseVersionIsNotCompatibleException(Version dbVersion, Version currentVersion)
        : base($"Database version is not compatible. Please migrate the database.\r\nDB Version: {dbVersion}\r\nCurrent Version: {currentVersion}")
    {
        DbVersion = dbVersion;
        CurrentVersion = currentVersion;
    }

    public Version DbVersion { get; }

    public Version CurrentVersion { get; }
}