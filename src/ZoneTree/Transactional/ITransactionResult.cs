namespace Tenray.ZoneTree.Transactional;

public interface ITransactionResult
{
    public bool IsAborted { get; }

    public bool Succeeded { get; }
}
