using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.Transactional;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests;

public class OptimisticTransactionTests
{
    [TestCase(0)]
    [TestCase(100000)]
    public void FirstTransaction(int compactionThreshold)
    {
        var dataPath = "data/FirstTransaction" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
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

        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    public void SeveralParallelTransactions(WriteAheadLogMode walMode)
    {
        var dataPath = "data/SeveralParallelTransactions." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        int n = 10000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreateTransactional();

        Parallel.For(0, n, (x) =>
        {
            var tx = zoneTree.BeginTransaction();
            zoneTree.Upsert(tx, x, x + x);
            zoneTree.Upsert(tx, -x, -x - x);
            zoneTree.Prepare(tx);
            zoneTree.Commit(tx);
        });

        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    public void SeveralParallelUpserts(WriteAheadLogMode walMode)
    {
        var dataPath = "data/SeveralParallelUpserts." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        int n = 10000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();

        Parallel.For(0, n, (x) =>
        {
            zoneTree.Upsert(x, x + x);
            zoneTree.Upsert(-x, -x - x);
        });

        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(0)]
    [TestCase(100000)]
    public void ReadCommittedTest(int compactionThreshold)
    {
        var dataPath = "data/ReadCommittedTest" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
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

        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(0)]
    [TestCase(100000)]
    public void TransactionLogCompactionTest(int compactionThreshold)
    {
        var dataPath = "data/TransactionLogCompactionTest" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
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
        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(0)]
    [TestCase(100000)]
    public void TransactionLogCompactionWithSkipWriteRule(int compactionThreshold)
    {
        var dataPath = "data/TransactionLogSkipWriteRule" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
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

        zoneTree.Maintenance.DestroyTree();
    }
}
