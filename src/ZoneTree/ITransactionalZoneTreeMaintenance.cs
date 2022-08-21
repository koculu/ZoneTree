using Tenray.ZoneTree.Transactional;

namespace Tenray.ZoneTree;

/// <summary>
/// The interface for the maintenance of a transactional ZoneTree.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public interface ITransactionalZoneTreeMaintenance<TKey, TValue>
{
    /// <summary>
    /// Underlying non-transactional ZoneTree.
    /// </summary>
    IZoneTree<TKey, TValue> ZoneTree { get; }

    /// <summary>
    /// Transaction Log instance.
    /// </summary>
    ITransactionLog<TKey, TValue> TransactionLog { get; }

    /// <summary>
    /// Retrieves uncommitted transaction ids.
    /// </summary>
    IReadOnlyList<long> UncommittedTransactionIds { get; }

    /// <summary>
    /// Saves tree meta data and clears the meta wal record.
    /// After calling this method, the JSON meta file contains 
    /// up to date tree meta data.
    /// 
    /// Saving meta file helps following:
    /// 1. Reduce the size of meta wal file.
    /// 2. Make Json meta file up to date to analyze parts of the LSM tree.
    /// Because Meta WAL file is not human readable.
    /// 
    /// It is up to user to decide when and how frequently save the meta file.
    /// </summary>
    void SaveMetaData();

    /// <summary>
    /// Removes the transactional tree from the universe.
    /// Destroys the tree, transaction logs, entire data and WAL store or folder.
    /// </summary>
    void DestroyTree();

    /// <summary>
    /// Rollbacks all uncommitted transactions.
    /// </summary>
    /// <returns>Count of rollbacked transactions.</returns>
    int RollbackAllUncommitted();

    /// <summary>
    /// Rollbacks all uncommitted transaction ids started before given date-time.
    /// Transaction log memory usage increases by state uncommitted transaction ids.
    /// Those must be rollbacked.
    /// </summary>
    /// <param name="dateTime">Max start time (exclusive)</param>
    /// <returns>Count of rollbacked transactions.</returns>
    int RollbackUncommittedTransactionIdsBefore(DateTime dateTime);
}