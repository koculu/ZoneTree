using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Transactional;

namespace Tenray.ZoneTree;

/// <summary>
/// The interface for the core functionality of a transactional ZoneTree.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
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
    /// Contains Key query without abort exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <returns></returns>
    TransactionResult<bool> ContainsKeyNoThrow(long transactionId, in TKey key);

    /// <summary>
    /// Tries to get the value of given key.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// <returns>true if key is found, otherwise false.</returns>
    /// <exception cref="TransactionAbortedException"></exception>
    bool TryGet(long transactionId, in TKey key, out TValue value);

    /// <summary>
    /// Tries to get the value of given key without abort exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// <returns>true if key is found, otherwise false.</returns>
    TransactionResult<bool> TryGetNoThrow(long transactionId, in TKey key, out TValue value);

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
    /// Upserts the key and value without abort exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// /// <returns>true if the key/value pair was inserted;
    /// false if the key/value pair was updated.</returns>
    TransactionResult<bool> UpsertNoThrow(long transactionId, in TKey key, in TValue value);

    /// <summary>
    /// Deletes the key.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    /// <exception cref="TransactionAbortedException"></exception>
    void Delete(long transactionId, in TKey key);

    /// <summary>
    /// Deletes the key without abort exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <param name="key">Key</param>
    TransactionResult DeleteNoThrow(long transactionId, in TKey key);

    /// <summary>
    /// Retrieves transaction state of given transaction id.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns>Transaction State</returns>
    TransactionState GetTransactionState(long transactionId);

    /// <summary>
    /// Begins the transaction. 
    /// Creates a transaction id and informs the transaction manager
    /// that a new transaction started.
    /// </summary>
    /// <returns>Transaction Id</returns>
    long BeginTransaction();

    /// <summary>
    /// Creates a fluent transaction.
    /// </summary>
    /// <returns>Fluent transaction.</returns>
    FluentTransaction<TKey, TValue> BeginFluentTransaction();

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    /// <exception cref="TransactionAbortedException"></exception>
    CommitResult Prepare(long transactionId);

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed. 
    /// This method does not throw transaction aborted exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    CommitResult PrepareNoThrow(long transactionId);

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// If commit is possible does the commit.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    /// <exception cref="TransactionAbortedException"></exception>
    CommitResult PrepareAndCommit(long transactionId);

    /// <summary>
    /// Prepares to Commit. Aborts transaction if needed.
    /// If commit is possible does the commit.
    /// This method does not throw transaction aborted exception.
    /// </summary>
    /// <param name="transactionId">Transaction Id</param>
    /// <returns></returns>
    CommitResult PrepareAndCommitNoThrow(long transactionId);

    /// <summary>
    /// Commits if the transaction is in ready to commit state.
    /// It is necessary to call PrepareCommit before calling this method.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsNotReadyToCommitException"></exception>
    /// <exception cref="TransactionAbortedException"></exception>
    CommitResult Commit(long transactionId);

    /// <summary>
    /// Commits if the transaction is in ready to commit state.
    /// It is necessary to call PrepareCommit before calling this method.
    /// This method does not throw transaction aborted exception.
    /// </summary>
    /// <param name="transactionId"></param>
    /// <returns></returns>
    /// <exception cref="TransactionIsNotReadyToCommitException"></exception>
    CommitResult CommitNoThrow(long transactionId);

    /// <summary>
    /// Rollbacks the transaction by undoing all writes by this transaction.
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
    /// If current transaction id given, 
    /// this method reads uncommitted key for given transaction
    /// and committed keys for other transactions.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="currentTransactionId">Current transaction.</param>
    /// <returns></returns>
    bool ReadCommittedContainsKey(in TKey key, long currentTransactionId = -1);

    /// <summary>
    /// Tries to get the value of given key without a transaction.
    /// This method avoids dirty reads.
    /// The query is executed on committed data.
    /// If current transaction id given, 
    /// this method reads uncommitted key for given transaction
    /// and committed keys for other transactions.
    /// </summary>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    /// <param name="currentTransactionId">Current transaction.</param>
    /// <returns></returns>
    bool ReadCommittedTryGet(in TKey key, out TValue value, long currentTransactionId = -1);

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

    /// <summary>
    /// Enables read-only mode.
    /// </summary>
    bool IsReadOnly { get; set; }
    
    /// <summary>
    /// ZoneTree Logger.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Creates the default ZoneTree Maintainer.
    /// </summary>
    IMaintainer CreateMaintainer();
}
