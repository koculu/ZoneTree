namespace Tenray.ZoneTree.Exceptions;

public sealed class CannotSetSerializersAfterTransactionLogInitializedException : ZoneTreeException
{
    public CannotSetSerializersAfterTransactionLogInitializedException()
        : base($"Can not set serializers after the transaction log is initialized.")
    {
    }
}
