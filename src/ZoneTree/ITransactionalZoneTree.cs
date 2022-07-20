using ZoneTree.Transactional;

namespace Tenray;

public interface ITransactionalZoneTree<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Contains Key query.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <exception cref="TransactionAbortedException"></exception>
    /// <returns></returns>
    bool ContainsKey(long transactionId, in TKey key);

    /// <summary>
    /// Tries to get the value of given key.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// <returns></returns>
    /// <exception cref="TransactionAbortedException"></exception>
    bool TryGet(long transactionId, in TKey key, out TValue value);

    /// <summary>
    /// Upserts the key and value.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// /// <returns>true if the key/value pair was inserted;
    /// false if the key/value pair was updated.</returns>
    /// <exception cref="TransactionAbortedException"></exception>
    bool Upsert(long transactionId, in TKey key, in TValue value);

    /// <summary>
    /// Deletes the key.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <exception cref="TransactionAbortedException"></exception>
    void Delete(long transactionId, in TKey key);

    /// <summary>
    /// Begins the transaction. 
    /// Creates a transaction id and informs the transaction manager
    /// that a new transaction started.
    /// </summary>
    /// <returns>Transaction Id</returns>
    long BeginTransaction();

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    /// <exception cref="TransactionAbortedException"></exception>
    CommitResult Prepare(long transactionId);

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// If commit is possible does the commit.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    /// <exception cref="TransactionAbortedException"></exception>
    CommitResult PrepareAndCommit(long transactionId);

    /// <summary>
    /// Commits if the transaction is in ready to commit state.
    /// It is necessary to call PrepareCommit before calling this method.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsNotReadyToCommitException"></exception>
    CommitResult Commit(long transactionId);

    /// <summary>
    /// Rollsback the transaction by undoing all writes by this transaction.
    /// </summary>
    /// <param name="transactionId"></param>
    void Rollback(long transactionId);

    /// <summary>
    /// Returns maintenance object belongs to this TransactionalZoneTree.
    /// </summary>
    ITransactionalZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    /// <summary>
    /// Contains Key query without a transaction.
    /// This method avoids dirty reads.
    /// The query is executed on committed data.
    /// </summary>
    /// <param name="key">Key</param>
    /// <returns></returns>
    bool ReadCommittedContainsKey(in TKey key);

    /// <summary>
    /// Tries to get the value of given key without a transaction.
    /// This method avoids dirty reads.
    /// The query is executed on committed data.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// <returns></returns>
    bool ReadCommittedTryGet(in TKey key, out TValue value);

    /// <summary>
    /// Starts a transaction,
    /// upserts the key-value,
    /// and commits the transaction.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    void UpsertAutoCommit(in TKey key, in TValue value);

    /// <summary>
    /// Starts a transaction,
    /// deletes the key-value,
    /// and commits the transaction.
    /// </summary>
    /// <param name="key">Key</param>
    void DeleteAutoCommit(in TKey key);

}
