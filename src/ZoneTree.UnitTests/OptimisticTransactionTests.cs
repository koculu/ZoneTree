using ZoneTree.Comparers;
using ZoneTree.Exceptions;
using ZoneTree.Options;
using ZoneTree.Serializers;
using ZoneTree.Transactional;

namespace ZoneTree.UnitTests;

public sealed class OptimisticTransactionTests
{
  [TestCase(0)]
  [TestCase(100000)]
  public void FirstTransaction(int compactionThreshold)
  {
    var dataPath = "data/FirstTransaction" + compactionThreshold;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .DisableDeletion()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .OpenOrCreateTransactional();

    zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

    var tx1 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx1, 3, 9);
    zoneTree.Prepare(tx1);
    var result = zoneTree.Commit(tx1);
    Assert.That(result.IsCommitted, Is.True);

    var tx2 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx2, 3, 10);

    var tx3 = zoneTree.BeginTransaction();
    zoneTree.TryGet(tx3, 3, out var value);
    Assert.That(value, Is.EqualTo(10));

    Assert.Throws<TransactionAbortedException>(() => zoneTree.Upsert(tx2, 3, 6));
    //tx2 is aborted. changes made by tx2 is rolled back.
    //tx3 depends on tx2 and it is also aborted.

    zoneTree.TryGet(tx3, 3, out value);
    Assert.That(value, Is.EqualTo(9));

    Assert.Throws<TransactionAbortedException>(() => zoneTree.PrepareAndCommit(tx2));

    Assert.Throws<TransactionAbortedException>(() => zoneTree.PrepareAndCommit(tx3));

    Assert.Throws<TransactionAlreadyCommittedException>(() => zoneTree.PrepareAndCommit(tx1));

    Assert.Throws<TransactionAlreadyCommittedException>(() => zoneTree.Commit(tx1));

    zoneTree.Maintenance.Drop();
  }

  [TestCase(WriteAheadLogMode.Sync)]
  [TestCase(WriteAheadLogMode.AsyncCompressed)]
  public void SeveralParallelTransactions(WriteAheadLogMode walMode)
  {
    var dataPath = "data/SeveralParallelTransactions." + walMode;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    int n = 10000;
    using var zoneTree = new ZoneTreeFactory<int, int>()
        .DisableDeletion()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .ConfigureWriteAheadLogOptions(x => x.WriteAheadLogMode = walMode)
        .OpenOrCreateTransactional();

    Parallel.For(0, n, (x) =>
    {
      var tx = zoneTree.BeginTransaction();
      zoneTree.Upsert(tx, x, x + x);
      zoneTree.Upsert(tx, -x, -x - x);
      zoneTree.Prepare(tx);
      zoneTree.Commit(tx);
    });

    zoneTree.Maintenance.Drop();
  }

  [TestCase(WriteAheadLogMode.Sync)]
  [TestCase(WriteAheadLogMode.AsyncCompressed)]
  public void SeveralParallelUpserts(WriteAheadLogMode walMode)
  {
    var dataPath = "data/SeveralParallelUpserts." + walMode;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    int n = 10000;
    using var zoneTree = new ZoneTreeFactory<int, int>()
        .DisableDeletion()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .ConfigureWriteAheadLogOptions(x => x.WriteAheadLogMode = walMode)
        .OpenOrCreate();

    Parallel.For(0, n, (x) =>
    {
      zoneTree.Upsert(x, x + x);
      zoneTree.Upsert(-x, -x - x);
    });

    zoneTree.Maintenance.Drop();
  }

  [TestCase(0)]
  [TestCase(100000)]
  public void ReadCommittedTest(int compactionThreshold)
  {
    var dataPath = "data/ReadCommittedTest" + compactionThreshold;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .OpenOrCreateTransactional();

    zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

    zoneTree.UpsertAutoCommit(3, 5);
    var tx1 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx1, 3, 9);
    zoneTree.Upsert(tx1, 7, 21);
    Assert.That(zoneTree.ReadCommittedContainsKey(3), Is.True);
    Assert.That(zoneTree.ReadCommittedTryGet(3, out var committed1), Is.True);
    Assert.That(committed1, Is.EqualTo(5));

    Assert.That(zoneTree.ReadCommittedContainsKey(7), Is.False);
    Assert.That(zoneTree.ReadCommittedTryGet(7, out var _), Is.False);

    zoneTree.PrepareAndCommit(tx1);

    Assert.That(zoneTree.ReadCommittedContainsKey(3), Is.True);
    Assert.That(zoneTree.ReadCommittedTryGet(3, out committed1), Is.True);
    Assert.That(committed1, Is.EqualTo(9));

    Assert.That(zoneTree.ReadCommittedContainsKey(7), Is.True);
    Assert.That(zoneTree.ReadCommittedTryGet(7, out var committed2), Is.True);
    Assert.That(committed2, Is.EqualTo(21));

    var tx2 = zoneTree.BeginTransaction();
    zoneTree.Delete(tx2, 7);

    Assert.That(zoneTree.ReadCommittedContainsKey(7), Is.True);
    Assert.That(zoneTree.ReadCommittedTryGet(7, out var committed3), Is.True);
    Assert.That(committed3, Is.EqualTo(21));

    zoneTree.PrepareAndCommit(tx2);

    Assert.That(zoneTree.ReadCommittedContainsKey(7), Is.False);
    Assert.That(zoneTree.ReadCommittedTryGet(7, out var _), Is.False);

    zoneTree.Maintenance.Drop();
  }

  [Test]
  public void AutoCommitIncrementalHistoryKeepsPreviousWriteStamp()
  {
    var dataPath = "data/AutoCommitIncrementalHistoryKeepsPreviousWriteStamp";
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    var transactionLog = new CapturingTransactionLog();
    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .ConfigureWriteAheadLogOptions(x => x.EnableIncrementalBackup = true)
        .SetTransactionLog(_ => transactionLog)
        .OpenOrCreateTransactional();

    zoneTree.UpsertAutoCommit(7, 11);
    zoneTree.UpsertAutoCommit(7, 13);
    zoneTree.DeleteAutoCommit(7);

    Assert.That(transactionLog.HistoryRecords.Count, Is.EqualTo(3));
    Assert.That(transactionLog.HistoryRecords[0].Value.Value2, Is.EqualTo(0));
    Assert.That(transactionLog.HistoryRecords[1].Value.Value2, Is.EqualTo(1));
    Assert.That(transactionLog.HistoryRecords[2].Value.Value2, Is.EqualTo(2));
    zoneTree.Maintenance.Drop();
  }

  [Test]
  public void RollbackKeepsOriginalHistoryWhenTransactionWritesSameKeyTwice()
  {
    var dataPath = "data/RollbackKeepsOriginalHistoryWhenTransactionWritesSameKeyTwice";
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .OpenOrCreateTransactional();

    zoneTree.UpsertAutoCommit(7, 10);
    var tx = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx, 7, 11);
    zoneTree.Upsert(tx, 7, 12);

    zoneTree.Rollback(tx);

    Assert.That(zoneTree.ReadCommittedTryGet(7, out var value), Is.True);
    Assert.That(value, Is.EqualTo(10));
    zoneTree.Maintenance.Drop();
  }

  [TestCase(0)]
  [TestCase(100000)]
  public void TransactionLogCompactionTest(int compactionThreshold)
  {
    var dataPath = "data/TransactionLogCompactionTest" + compactionThreshold;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .OpenOrCreateTransactional();

    zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

    var tx1 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx1, 5, 11);
    zoneTree.Upsert(tx1, 6, 13);

    var tx2 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx2, 5, 12);
    zoneTree.Upsert(tx2, 6, 14);

    zoneTree.PrepareAndCommit(tx2);

    Assert.That(zoneTree.ReadCommittedTryGet(5, out var v5), Is.True);
    Assert.That(v5, Is.EqualTo(12));

    Assert.That(zoneTree.ReadCommittedTryGet(6, out var v6), Is.True);
    Assert.That(v6, Is.EqualTo(14));

    var tx3 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx3, 6, 14);
    zoneTree.Rollback(tx1);

    Assert.That(zoneTree.ReadCommittedTryGet(5, out v5), Is.True);
    Assert.That(v5, Is.EqualTo(12));

    Assert.That(zoneTree.ReadCommittedTryGet(6, out v6), Is.True);
    Assert.That(v6, Is.EqualTo(14));

    zoneTree.Rollback(tx3);
    zoneTree.Maintenance.Drop();
  }

  [TestCase(0)]
  [TestCase(100000)]
  public void TransactionLogCompactionWithSkipWriteRule(int compactionThreshold)
  {
    var dataPath = "data/TransactionLogSkipWriteRule" + compactionThreshold;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .OpenOrCreateTransactional();

    zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

    var tx1 = zoneTree.BeginTransaction();
    var tx2 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx2, 5, 12);
    zoneTree.PrepareAndCommit(tx2);

    // start new transaction to compact transaction log.
    var tx3 = zoneTree.BeginTransaction();

    // the following write should be skipped bcs of tx2 write.
    zoneTree.Upsert(tx1, 5, 13);
    zoneTree.PrepareAndCommit(tx1);

    Assert.That(zoneTree.ReadCommittedTryGet(5, out var v5), Is.True);
    Assert.That(v5, Is.EqualTo(12));

    zoneTree.Maintenance.Drop();
  }

  [TestCase(0)]
  [TestCase(100000)]
  public void TransactionIsolationTest(int compactionThreshold)
  {
    var dataPath = "data/TransactionIsolationTest" + compactionThreshold;
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    using var zoneTree = new ZoneTreeFactory<int, int>()
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .SetIsDeletedDelegate((in int key, in int value) => value == -1)
        .SetMarkValueDeletedDelegate((ref int value) => value = -1)
        .OpenOrCreateTransactional();

    zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

    var tx1 = zoneTree.BeginTransaction();
    var tx2 = zoneTree.BeginTransaction();
    zoneTree.Upsert(tx1, 9, 21);
    zoneTree.Upsert(tx2, 5, 12);

    Assert.IsTrue(zoneTree.TryGet(tx2, 9, out var v9));
    Assert.IsTrue(zoneTree.TryGet(tx1, 9, out v9));
    Assert.That(v9, Is.EqualTo(21));

    Assert.IsTrue(zoneTree.TryGetNoThrow(tx1, 5, out var v5).IsAborted);
    Assert.IsTrue(zoneTree.TryGetNoThrow(tx2, 5, out v5).Succeeded);
    Assert.That(v5, Is.EqualTo(12));

    // tx2 depends on tx1 bcs of read of key 9. tx1 aborted and so tx2.
    Assert.That(zoneTree.PrepareAndCommitNoThrow(tx2).IsAborted, Is.True);

    zoneTree.Maintenance.Drop();
  }

  sealed class CapturingTransactionLog : ITransactionLog<int, int>
  {
    long nextTransactionId;

    readonly Dictionary<long, TransactionState> TransactionStates = new();

    readonly Dictionary<int, ReadWriteStamp> ReadWriteStamps = new();

    public List<(long TransactionId, int Key, CombinedValue<int, long> Value)> HistoryRecords { get; } = new();

    public int CompactionThreshold { get; set; }

    public int TransactionCount => TransactionStates.Count;

    public IReadOnlyList<long> TransactionIds => TransactionStates.Keys.ToArray();

    public IReadOnlyList<long> UncommittedTransactionIds => TransactionStates
        .Where(x => x.Value == TransactionState.Uncommitted)
        .Select(x => x.Key)
        .ToArray();

    public long GetNextTransactionId()
    {
      return ++nextTransactionId;
    }

    public void TransactionStarted(long transactionId)
    {
      TransactionStates[transactionId] = TransactionState.Uncommitted;
    }

    public void TransactionCommitted(long transactionId)
    {
      TransactionStates[transactionId] = TransactionState.Committed;
    }

    public void TransactionAborted(long transactionId)
    {
      TransactionStates[transactionId] = TransactionState.Aborted;
    }

    public TransactionState GetTransactionState(long transactionId)
    {
      return TransactionStates.TryGetValue(transactionId, out var state) ?
          state :
          TransactionState.Committed;
    }

    public TransactionMeta GetTransactionMeta(long transactionId)
    {
      return new TransactionMeta
      {
        State = GetTransactionState(transactionId)
      };
    }

    public void AddDependency(long src, long dest)
    {
    }

    public IReadOnlyList<long> GetDependencyList(long transactionId)
    {
      return Array.Empty<long>();
    }

    public void AddHistoryRecord(
        long transactionId,
        int key,
        CombinedValue<int, long> combinedValue)
    {
      HistoryRecords.Add((transactionId, key, combinedValue));
    }

    public IDictionary<int, CombinedValue<int, long>> GetHistory(long transactionId)
    {
      return HistoryRecords
          .Where(x => x.TransactionId == transactionId)
          .ToDictionary(x => x.Key, x => x.Value);
    }

    public bool TryGetReadWriteStamp(in int key, out ReadWriteStamp readWriteStamp)
    {
      return ReadWriteStamps.TryGetValue(key, out readWriteStamp);
    }

    public bool AddOrUpdateReadWriteStamp(in int key, in ReadWriteStamp readWriteStamp)
    {
      ReadWriteStamps[key] = readWriteStamp;
      return true;
    }

    public IReadOnlyList<long> GetUncommittedTransactionIdsBefore(DateTime dateTime)
    {
      return UncommittedTransactionIds;
    }

    public void Dispose()
    {
    }
  }
}
