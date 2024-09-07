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

        void ReloadData()
        {
            using var zoneTree = new ZoneTreeFactory<int, int>()
                .SetDataDirectory(dataPath)
                .SetMutableSegmentMaxItemCount(100)
                .Open();

            var opIndex = zoneTree.Upsert(recordCount + 1, recordCount + 1);
            Assert.That(opIndex, Is.EqualTo(recordCount + 1));
            zoneTree.Maintenance.Drop();
        }
        CreateData();
        ReloadData();
        Assert.IsTrue(
            opIndexes.Order().ToArray()
            .SequenceEqual(Enumerable.Range(1, recordCount).Select(x => (long)x)));
    }
}
