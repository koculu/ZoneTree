using Tenray;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public class IteratorTests
{
    [Test]
    public void IntIntIterator()
    {
        var dataPath = "data/IntIntIterator";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new IntegerComparerAscending())
            .SetMutableSegmentMaxItemCount(11)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = 0; i < a; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().AsTask().Wait();

        zoneTree.ForceDelete(127);
        zoneTree.ForceDelete(19);
        zoneTree.ForceDelete(20);
        zoneTree.ForceDelete(21);

        for (var i = a; i < b; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        using var iterator = zoneTree.CreateIterator(true);
        
        for (var i = 0; i < b; ++i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            iterator.Next();
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
        }
        
        Assert.That(iterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b - 4));

        using var reverseIterator = zoneTree.CreateReverseIterator(true);

        for (var i = b - 1; i >= 0; --i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            reverseIterator.Next();
            Assert.That(reverseIterator.CurrentKey, Is.EqualTo(i));
            Assert.That(reverseIterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(reverseIterator.Next(), Is.False);

        zoneTree.Maintenance.MoveSegmentZeroForward();
        zoneTree.Maintenance.StartMergeOperation().AsTask().Wait();

        Assert.That(zoneTree.Maintenance.DiskSegment.Length, Is.EqualTo(b - 4));        
        zoneTree.Maintenance.SaveMetaData(); 
        iterator.Dispose();
        reverseIterator.Dispose();
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void IntIntIteratorSeek()
    {
        var dataPath = "data/IntIntIteratorSeek";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new IntegerComparerAscending())
            .SetMutableSegmentMaxItemCount(11)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = 0; i < a; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().AsTask().Wait();

        zoneTree.ForceDelete(127);
        zoneTree.ForceDelete(19);
        zoneTree.ForceDelete(20);
        zoneTree.ForceDelete(21);

        for (var i = a; i < b; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        using var iterator = zoneTree.CreateIterator(true);
        iterator.Seek(13);        
        for (var i = 13; i < b; ++i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            iterator.Next();
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(iterator.Next(), Is.False);

        iterator.Seek(20);
        for (var i = 22; i < b; ++i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            iterator.Next();
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(iterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b - 4));
        iterator.SeekFirst();
        Assert.That(iterator.Next(), Is.True);
        Assert.That(iterator.Next(), Is.True);
        Assert.That(iterator.CurrentKey, Is.EqualTo(1));

        using var reverseIterator = zoneTree.CreateReverseIterator(true);
        reverseIterator.Seek(451);
        for (var i = 451; i >= 0; --i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            reverseIterator.Next();
            Assert.That(reverseIterator.CurrentKey, Is.EqualTo(i));
            Assert.That(reverseIterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(reverseIterator.Next(), Is.False);
        reverseIterator.SeekFirst();
        Assert.That(reverseIterator.Next(), Is.True);
        Assert.That(reverseIterator.Next(), Is.True);
        Assert.That(reverseIterator.CurrentKey, Is.EqualTo(b-2));
        Assert.That(zoneTree.Count(), Is.EqualTo(b - 4));

        zoneTree.Maintenance.MoveSegmentZeroForward();
        zoneTree.Maintenance.StartMergeOperation().AsTask().Wait();

        Assert.That(zoneTree.Maintenance.DiskSegment.Length, Is.EqualTo(b - 4));
        zoneTree.Maintenance.SaveMetaData();
        iterator.Dispose();
        reverseIterator.Dispose();
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void IntIntIteratorReflectNewInserts()
    {
        var dataPath = "data/IntIntIteratorReflectNewInserts";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetComparer(new IntegerComparerAscending())
            .SetMutableSegmentMaxItemCount(250)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 251;
        var b = 500;
        for (var i = 1; i < a; i += 2)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().AsTask().Wait();

        for (var i = a; i < b; i += 2)
        {
            zoneTree.Upsert(i, i + i);
        }
        zoneTree.ForceDelete(11);
        zoneTree.ForceDelete(13);
        zoneTree.ForceDelete(15);
        using var iterator = zoneTree.CreateIterator(true);
        iterator.Seek(13);
        zoneTree.Upsert(24, 48);
        for (var i = 17; i < b; ++i)
        {
            if (i != 24 && i % 2 == 0)
                ++i;
            iterator.Next();
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(iterator.Next(), Is.False);
        Assert.That(iterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b/2 - 2));
        zoneTree.Maintenance.SaveMetaData();
        iterator.Dispose();
        zoneTree.Maintenance.DestroyTree();
    }
}