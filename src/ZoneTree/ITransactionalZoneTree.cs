using ZoneTree.Transactional;

namespace Tenray;

public interface ITransactionalZoneTree<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Underlying non-transactional ZoneTree.
    /// </summary>
    IZoneTree<TKey, TValue> ZoneTree { get; }

    /// <summary>
    /// Contains Key query.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <param name="key"></param>
    /// <exception cref="TransactionIsAbortedException"></exception>
    /// <returns></returns>
    bool ContainsKey(long transactionId, in TKey key);

    /// <summary>
    /// Tries to get the value of given key.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsAbortedException"></exception>
    bool TryGet(long transactionId, in TKey key, out TValue value);

    /// <summary>
    /// Upserts the key and value.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsAbortedException"></exception>
    bool Upsert(long transactionId, in TKey key, in TValue value);

    /// <summary>
    /// Deletes the key.
    /// <exception cref="TransactionIsAbortedException"></exception>
    /// </summary>
    /// <param name="transactionId"></param>
    /// <param name="key"></param>
    void Delete(long transactionId, in TKey key);

    /// <summary>
    /// Begins the transaction. 
    /// Creates a transaction id and informs the transaction manager
    /// that a new transaction started.
    /// </summary>
    /// <returns>Transaction id</returns>
    long BeginTransaction();

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsAbortedException"></exception>
    TransactionCommitResult Prepare(long transactionId);

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// If commit is possible does the commit.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsAbortedException"></exception>
    TransactionCommitResult PrepareAndCommit(long transactionId);

    /// <summary>
    /// Commits if the transaction is in ready to commit state.
    /// It is necessary to call PrepareCommit before calling this method.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsNotReadyToCommitException"></exception>
    TransactionCommitResult Commit(long transactionId);

    /// <summary>
    /// Rollsback the transaction by undoing all writes by this transaction.
    /// </summary>
    /// <param name="transactionId"></param>
    void Rollback(long transactionId);
}
