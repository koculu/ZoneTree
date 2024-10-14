using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.UnitTests;

public sealed class ReplicatorTests
{
    [Test]
    public void TestReplicator()
    {
        var dataPath = "data/TestReplicator";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var recordCount = 50_000;
        var keyCount = 15_000;
        var maxMemory = 10_000;
        void CreateData()
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath + "/source")
                .SetMutableSegmentMaxItemCount(maxMemory)
                .OpenOrCreate();

            using var replica = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath + "/replica")
                .SetMutableSegmentMaxItemCount(maxMemory)
                .OpenOrCreate();

            var replicator = new Replicator<int, int>(replica, dataPath + "/replica-op-index");
            using var maintainer1 = zoneTree.CreateMaintainer();
            using var maintainer2 = replica.CreateMaintainer();
            var random = new Random();
            int replicated = 0;
            Parallel.For(0, recordCount, (i) =>
            {
                var key = i % keyCount;
                var value = random.Next();
                var opIndex = zoneTree.Upsert(key, value);
                Task.Run(() =>
                {
                    replicator.OnUpsert(key, value, opIndex);
                    Interlocked.Increment(ref replicated);
                });
            });
            while (replicated < recordCount) Task.Delay(500).Wait();
            maintainer1.EvictToDisk();
            maintainer2.EvictToDisk();
            maintainer1.WaitForBackgroundThreads();
            maintainer2.WaitForBackgroundThreads();
        }

        void TestEqual()
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath + "/source")
                .Open();

            using var replica = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath + "/replica")
                .Open();

            using var iterator1 = zoneTree.CreateIterator();
            using var iterator2 = replica.CreateIterator();
            while (true)
            {
                var n1 = iterator1.Next();
                var n2 = iterator2.Next();
                Assert.That(n2, Is.EqualTo(n1));
                if (!n1) break;
                Assert.That(iterator2.Current, Is.EqualTo(iterator1.Current));
            }
            zoneTree.Maintenance.Drop();
            replica.Maintenance.Drop();
        }

        CreateData();
        TestEqual();
    }
}
