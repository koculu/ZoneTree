using System.Collections.Concurrent;
using Tenray.ZoneTree.PresetTypes;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.UnitTests;

public sealed class OpIndexTests
{
    [Test]
    public void TestOpIndex()
    {
        var dataPath = "data/TestOpIndex";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var recordCount = 10_000;
        var opIndexes = new ConcurrentBag<long>();
        void CreateData()
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath)
                .SetMutableSegmentMaxItemCount(100)
                .OpenOrCreate();

            using var maintainer = zoneTree.CreateMaintainer();
            Parallel.For(0, recordCount, (i) =>
            {
                var opIndex = zoneTree.Upsert(i, i);
                opIndexes.Add(opIndex);
            });
            maintainer.EvictToDisk();
            maintainer.WaitForBackgroundThreads();
        }

        void ReloadData(int expectedIndex, bool drop = false)
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath)
                .SetMutableSegmentMaxItemCount(100)
                .Open();

            var opIndex = zoneTree.Upsert(expectedIndex, expectedIndex);
            Assert.That(opIndex, Is.EqualTo(expectedIndex));
            if (drop)
                zoneTree.Maintenance.Drop();
        }

        CreateData();
        ReloadData(recordCount + 1);
        ReloadData(recordCount + 2);
        ReloadData(recordCount + 3, true);

        Assert.IsTrue(
            opIndexes.Order().ToArray()
            .SequenceEqual(Enumerable.Range(1, recordCount).Select(x => (long)x)));
    }

    [Test]
    public void TestOpIndex2()
    {
        var dataPath = "data/TestOpIndex2";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var recordCount = 10_000;
        var opIndexes = new ConcurrentBag<long>();
        void CreateData()
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath)
                .SetMutableSegmentMaxItemCount(100)
                .OpenOrCreate();

            using var maintainer = zoneTree.CreateMaintainer();
            Parallel.For(0, recordCount, (i) =>
            {
                var opIndex = zoneTree.Upsert(i, i);
                opIndexes.Add(opIndex);
            });
            maintainer.EvictToDisk();
            maintainer.WaitForBackgroundThreads();
        }

        void ReloadData(int expectedIndex, bool drop = false)
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath)
                .SetMutableSegmentMaxItemCount(100)
                .Open();

            var opIndex = zoneTree.Upsert(expectedIndex, expectedIndex);
            Assert.That(opIndex, Is.EqualTo(expectedIndex));

            using var maintainer = zoneTree.CreateMaintainer();
            maintainer.EvictToDisk();
            maintainer.WaitForBackgroundThreads();

            if (drop)
                zoneTree.Maintenance.Drop();
        }

        CreateData();
        ReloadData(recordCount + 1);
        ReloadData(recordCount + 2);
        ReloadData(recordCount + 3, true);

        Assert.IsTrue(
            opIndexes.Order().ToArray()
            .SequenceEqual(Enumerable.Range(1, recordCount).Select(x => (long)x)));
    }
}
