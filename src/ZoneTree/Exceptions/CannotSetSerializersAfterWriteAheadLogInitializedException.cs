namespace Tenray.ZoneTree.Exceptions;

public class CannotSetSerializersAfterWriteAheadLogProviderInitializedException : ZoneTreeException
{
    public CannotSetSerializersAfterWriteAheadLogProviderInitializedException()
        : base($"Can not set serializers after the write ahead log provider is initialized.")
    {
    }
}
