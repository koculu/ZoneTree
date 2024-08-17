using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.Transactional;

namespace Tenray.ZoneTree.UnitTests;

public sealed class ExceptionlessTransactionTests
{
    [TestCase(0)]
    [TestCase(100000)]
    public void TransactionWithNoThrowAPI(int compactionThreshold)
    {
        var dataPath = "data/TransactionWithNoThrowAPI" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .DisableDeletion()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .OpenOrCreateTransactional();

        zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

        var tx1 = zoneTree.BeginTransaction();
        Assert.That(zoneTree.UpsertNoThrow(tx1, 3, 9).Succeeded, Is.True);
        zoneTree.PrepareNoThrow(tx1);
        var result = zoneTree.CommitNoThrow(tx1);
        Assert.That(result.IsCommitted, Is.True);

        var tx2 = zoneTree.BeginTransaction();
        zoneTree.UpsertNoThrow(tx2, 3, 10);

        var tx3 = zoneTree.BeginTransaction();
        zoneTree.TryGetNoThrow(tx3, 3, out var value);
        Assert.That(value, Is.EqualTo(10));

        Assert.That(zoneTree.UpsertNoThrow(tx2, 3, 6).IsAborted, Is.True);
        //tx2 is aborted. changes made by tx2 is rolled back.
        //tx3 depends on tx2 and it is also aborted.

        zoneTree.TryGetNoThrow(tx3, 3, out value);
        Assert.That(value, Is.EqualTo(9));

        Assert.That(zoneTree.PrepareAndCommitNoThrow(tx2).IsAborted, Is.True);

        Assert.That(zoneTree.PrepareAndCommitNoThrow(tx3).IsAborted, Is.True);

        Assert.Throws<TransactionAlreadyCommittedException>(() => zoneTree.PrepareAndCommitNoThrow(tx1));

        Assert.Throws<TransactionAlreadyCommittedException>(() => zoneTree.CommitNoThrow(tx1));

        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(0)]
    [TestCase(100000)]
    public async Task TransactionWithFluentAPI(int compactionThreshold)
    {
        var dataPath = "data/TransactionWithFluentAPI" + compactionThreshold;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .DisableDeletion()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogOptions(x =>
            {
                // interval sleep count set to zero
                // to prevent sleep intervals sum up on replacement.
                if (compactionThreshold == 0)
                {
                    x.WriteAheadLogMode = WriteAheadLogMode.Sync;
                    x.AsyncCompressedModeOptions.EmptyQueuePollInterval = 0;
                    x.SyncCompressedModeOptions.TailWriterJobInterval = 0;
                }
            })
            .OpenOrCreateTransactional();

        zoneTree.Maintenance.TransactionLog.CompactionThreshold = compactionThreshold;

        var random = new Random();
        await Parallel.ForEachAsync(Enumerable.Range(0, 1000), async (x, cancel) =>
        {
            using var transaction =
            zoneTree
            .BeginFluentTransaction()
            .Do((tx) => zoneTree.UpsertNoThrow(tx, 3, 9))
            .Do((tx) =>
            {
                if (zoneTree.TryGetNoThrow(tx, 3, out var value).IsAborted)
                    return TransactionResult.Aborted();
                if (zoneTree.UpsertNoThrow(tx, 3, 9).IsAborted)
                    return TransactionResult.Aborted();
                return TransactionResult.Success();
            })
            .SetRetryCountForPendingTransactions(100)
            .SetRetryCountForAbortedTransactions(10);
            await transaction.CommitAsync();
            if (transaction.TotalAbortRetried > 0)
                Console.WriteLine("abort:" + transaction.TotalAbortRetried);
            if (transaction.TotalPendingTransactionsRetried > 0)
                Console.WriteLine("pending:" + transaction.TotalPendingTransactionsRetried);
        });

        zoneTree.Maintenance.DestroyTree();
    }
}