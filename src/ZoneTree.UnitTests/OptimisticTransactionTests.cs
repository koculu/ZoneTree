using Tenray;
using ZoneTree.Serializers;

namespace ZoneTree.UnitTests;

public class OptimisticTransactionTests
{
    [Test]
    public void FirstTransaction()
    {
        var dataPath = "data/FirstTransaction";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreateTransactional();

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

        zoneTree.DestroyTree();
    }

    [Test]
    public void SeveralParallelTransactions()
    {
        var dataPath = "data/SeveralParallelTransactions";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        int n = 10000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
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

        zoneTree.DestroyTree();
    }


    [Test]
    public void SeveralParallelUpserts()
    {
        var dataPath = "data/SeveralParallelUpserts";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        int n = 10000;
        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
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
}