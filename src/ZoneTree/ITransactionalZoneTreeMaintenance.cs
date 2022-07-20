namespace Tenray.ZoneTree;

public interface ITransactionalZoneTreeMaintenance<TKey, TValue>
{
    /// <summary>
    /// Underlying non-transactional ZoneTree.
    /// </summary>
    IZoneTree<TKey, TValue> ZoneTree { get; }

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
    /// Aborts and rollback all uncommitted transactions.
    /// </summary>
    void RollbackUncommittedTransactions();
}