namespace Tenray.ZoneTree.Transactional;

public struct TransactionResult : ITransactionResult
{
    public bool IsAborted { get; }

    public bool Succeeded => !IsAborted;

    public TransactionResult(bool isAborted)
    {
        IsAborted = isAborted;
    }

    public static TransactionResult Aborted()
    {
        return new TransactionResult(true);
    }

    public static TransactionResult Success()
    {
        return new TransactionResult(false);
    }
}

public struct TransactionResult<TType> : ITransactionResult
{
    public TType Result { get; }

    public bool IsAborted { get; }
    
    public bool Succeeded => !IsAborted;

    public TransactionResult()
    {
        IsAborted = true;
        Result = default;
    }

    public TransactionResult(TType result)
    {
        IsAborted = false;
        Result = result;
    }

    public static TransactionResult<TType> Aborted()
    {
        return new TransactionResult<TType>();
    }

    public static TransactionResult<TType> Success(TType result)
    {
        return new TransactionResult<TType>(result);
    }
}
