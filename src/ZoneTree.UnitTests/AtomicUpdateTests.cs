using Tenray;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public class AtomicUpdateTests
{
    [Test]
    public void IntIntAtomicIncrement()
    {
        var dataPath = "data/IntIntAtomicIncrement";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new IntegerComparerDescending())
            .SetMutableSegmentMaxItemCount(500)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        for (var i = 0; i < 2000; ++i)
        {
            data.Upsert(i, i + i);
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().AsTask().Wait();
        var random = new Random();
        var off = -1;
        Parallel.For(0, 1001, (x) =>
        {
            try
            {
                var len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.Upsert(i, i + i);
                }
                len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.TryAtomicAddOrUpdate(3999, 0, (y) => y + 1);
                    Interlocked.Increment(ref off);
                }

            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        });

        for (var i = 0; i < 2000; ++i)
        {
            var result = data.TryGet(i, out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo(i + i));
            Assert.That(data.ContainsKey(i), Is.True);
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().AsTask().Wait();
        data.TryGet(3999, out var finalValue);
        Assert.That(finalValue, Is.EqualTo(off));
        data.Maintenance.DestroyTree();
    }

    [Test]
    public void IntIntAtomicIncrementForSkipList()
    {
        var dataPath = "data/IntIntAtomicIncrementForSkipList";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new IntegerComparerDescending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        for (var i = 0; i < 2000; ++i)
        {
            data.Upsert(i, i + i);
        }
        var random = new Random();
        var off = -1;
        Parallel.For(0, 1001, (x) =>
        {
            try
            {
                var len = random.Next(300);
                for (var i = 0; i < len; ++i)
                {
                    data.Upsert(i, i + i);
                }
                len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.TryAtomicAddOrUpdate(3999, 0, (y) => y + 1);
                    Interlocked.Increment(ref off);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        });

        for (var i = 0; i < 2000; ++i)
        {
            var result = data.TryGet(i, out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo(i + i));
            Assert.That(data.ContainsKey(i), Is.True);
        }
        data.TryGet(3999, out var finalValue);
        Assert.That(finalValue, Is.EqualTo(off));
        data.Maintenance.DestroyTree();
    }
}