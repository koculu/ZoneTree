using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests;

public class AtomicUpdateTests
{
    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    [TestCase(WriteAheadLogMode.CompressedImmediate)]
    public void IntIntAtomicIncrement(WriteAheadLogMode walMode)
    {
        var dataPath = "data/IntIntAtomicIncrement." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var counterKey = -3999;
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerDescending())
            .SetMutableSegmentMaxItemCount(500)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        for (var i = 0; i < 2000; ++i)
        {
            data.Upsert(i, i + i);
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().Join();
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
                    data.TryAtomicAddOrUpdate(counterKey, 0, (y) => y + 1);
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
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().Join();
        data.TryGet(counterKey, out var finalValue);
        Assert.That(finalValue, Is.EqualTo(off));
        data.Maintenance.DestroyTree();
    }

    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    [TestCase(WriteAheadLogMode.CompressedImmediate)]
    public void IntIntAtomicIncrementForBTree(WriteAheadLogMode walMode)
    {
        var dataPath = "data/IntIntAtomicIncrementForBTree." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerDescending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
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

    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    [TestCase(WriteAheadLogMode.CompressedImmediate)]
    public void IntIntMutableSegmentOnlyAtomicIncrement(WriteAheadLogMode walMode)
    {
        var dataPath = "data/IntIntMutableSegmentOnlyAtomicIncrement." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var counterKey = -3999;
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerDescending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
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
                var len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.Upsert(i, i + i);
                }
                len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.TryAtomicAddOrUpdate(counterKey, 0, (y) => y + 1);
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
        data.TryGet(counterKey, out var finalValue);
        Assert.That(finalValue, Is.EqualTo(off));
        data.Maintenance.DestroyTree();
    }

    [TestCase(WriteAheadLogMode.Immediate)]
    [TestCase(WriteAheadLogMode.Lazy)]
    [TestCase(WriteAheadLogMode.CompressedImmediate)]
    public void IntIntMutableSegmentSeveralUpserts(WriteAheadLogMode walMode)
    {
        var dataPath = "data/IntIntMutableSegmentSeveralUpserts." + walMode;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerDescending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .ConfigureWriteAheadLogProvider(x => x.WriteAheadLogMode = walMode)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        var n = 1000;
        var random = new Random();
        Parallel.For(0, 1000, (x) =>
        {
            try
            {
                var len = random.Next(n);
                for (var i = 0; i < len; ++i)
                {
                    var k = random.Next();
                    data.Upsert(k, k + k);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        });
        using var iterator = data.CreateIterator(IteratorType.NoRefresh);
        while (iterator.Next())
        {
            var k = iterator.CurrentKey;
            var v = iterator.CurrentValue;
            Assert.That(v, Is.EqualTo(k + k));
            Assert.That(data.ContainsKey(k), Is.True);
        }
        data.Maintenance.DestroyTree();
    }
}