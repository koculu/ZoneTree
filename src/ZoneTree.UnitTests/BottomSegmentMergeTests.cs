using System.Text.Json;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.UnitTests;

public class BottomSegmentMergeTests
{
    [Test]
    public void IntIntBottomMerge()
    {
        var dataPath = "data/IntIntBottomMerge";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var zoneTree = new ZoneTreeFactory<int, int>()
            .SetDiskSegmentMaxItemCount(10)
            .SetDataDirectory(dataPath)
            .ConfigureDiskSegmentOptions(
                x => x.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment)
            .OpenOrCreate();
        var m = zoneTree.Maintenance;
        var insertCount = 100;
        var p = 10;
        for (var i = 0; i < insertCount; ++i)
        {
            zoneTree.Upsert(i, i + i);
            if (i % p == p - 1)
            {
                m.MoveMutableSegmentForward();
                m.StartMergeOperation().Join();
                for (var j = 0; j < p - 10; ++j)
                {
                    ++i;
                    if (i >= insertCount)
                        break;
                    zoneTree.Upsert(i, i + i);
                }
                ++p;
            }
        }

        var expected1 = new long[] { 11, 13, 15, 17, 19, 21 };
        var expected2 = new long[] { 11, 13, 51, 21 };

        var sum = 
            m.BottomSegments.Sum(x => x.Length) +
            m.InMemoryRecordCount +
            m.DiskSegment.Length;
        var lens = m.BottomSegments.Select(x => x.Length).ToArray();
        Assert.That(sum, Is.EqualTo(insertCount));
        Assert.That(lens, Is.EqualTo(expected1));
        m.StartBottomSegmentsMergeOperation(1, 3).Join();

        lens = m.BottomSegments.Select(x => x.Length).ToArray();
        Assert.That(lens, Is.EqualTo(expected2));
        zoneTree.Dispose();

        zoneTree = new ZoneTreeFactory<int, int>()
            .SetDiskSegmentMaxItemCount(10)
            .SetDataDirectory(dataPath)
            .OpenOrCreate();

        lens = m.BottomSegments.Select(x => x.Length).ToArray();
        Assert.That(lens, Is.EqualTo(expected2));
        zoneTree.Maintenance.DestroyTree();
        zoneTree.Dispose();
    }
}