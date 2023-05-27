namespace Tenray.ZoneTree.Transactional;

public readonly struct TransactionResult : ITransactionResult, IEquatable<TransactionResult>
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

    public override bool Equals(object obj)
    {
        return obj is TransactionResult result && Equals(result);
    }

    public bool Equals(TransactionResult other)
    {
        return IsAborted == other.IsAborted &&
               Succeeded == other.Succeeded;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsAborted, Succeeded);
    }

    public static bool operator ==(TransactionResult left, TransactionResult right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TransactionResult left, TransactionResult right)
    {
        return !(left == right);
    }
}

public readonly struct TransactionResult<TType> : ITransactionResult, IEquatable<TransactionResult<TType>>
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

    public override bool Equals(object obj)
    {
        return obj is TransactionResult<TType> result && Equals(result);
    }

    public bool Equals(TransactionResult<TType> other)
    {
        return EqualityComparer<TType>.Default.Equals(Result, other.Result) &&
               IsAborted == other.IsAborted &&
               Succeeded == other.Succeeded;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Result, IsAborted, Succeeded);
    }

    public static bool operator ==(TransactionResult<TType> left, TransactionResult<TType> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TransactionResult<TType> left, TransactionResult<TType> right)
    {
        return !(left == right);
    }
}
