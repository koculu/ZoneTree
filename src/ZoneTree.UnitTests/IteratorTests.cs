﻿using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.UnitTests;

public sealed class IteratorTests
{
    [Test]
    public void IntIntIterator()
    {
        var dataPath = "data/IntIntIterator";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetMutableSegmentMaxItemCount(11)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = 0; i < a; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().Join();

        zoneTree.ForceDelete(127);
        zoneTree.ForceDelete(19);
        zoneTree.ForceDelete(20);
        zoneTree.ForceDelete(21);

        for (var i = a; i < b; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        using var iterator = zoneTree.CreateIterator(IteratorType.AutoRefresh);

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

        using var reverseIterator = zoneTree.CreateReverseIterator(IteratorType.AutoRefresh);

        for (var i = b - 1; i >= 0; --i)
        {
            if (i == 19 || i == 20 || i == 21 || i == 127)
                continue;
            reverseIterator.Next();
            Assert.That(reverseIterator.CurrentKey, Is.EqualTo(i));
            Assert.That(reverseIterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(reverseIterator.Next(), Is.False);

        zoneTree.Maintenance.MoveMutableSegmentForward();
        zoneTree.Maintenance.StartMergeOperation().Join();

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
            .SetMutableSegmentMaxItemCount(11)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = 0; i < a; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().Join();

        zoneTree.ForceDelete(127);
        zoneTree.ForceDelete(19);
        zoneTree.ForceDelete(20);
        zoneTree.ForceDelete(21);

        for (var i = a; i < b; ++i)
        {
            zoneTree.Upsert(i, i + i);
        }

        using var iterator = zoneTree.CreateIterator(IteratorType.AutoRefresh);
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

        using var reverseIterator = zoneTree.CreateReverseIterator(IteratorType.AutoRefresh);
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
        Assert.That(reverseIterator.CurrentKey, Is.EqualTo(b - 2));
        Assert.That(zoneTree.Count(), Is.EqualTo(b - 4));

        zoneTree.Maintenance.MoveMutableSegmentForward();
        zoneTree.Maintenance.StartMergeOperation().Join();

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
            .SetMutableSegmentMaxItemCount(250)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var a = 251;
        var b = 500;
        for (var i = 1; i < a; i += 2)
        {
            zoneTree.Upsert(i, i + i);
        }

        zoneTree.Maintenance.StartMergeOperation().Join();

        for (var i = a; i < b; i += 2)
        {
            zoneTree.Upsert(i, i + i);
        }
        zoneTree.ForceDelete(11);
        zoneTree.ForceDelete(13);
        zoneTree.ForceDelete(15);
        using var iterator = zoneTree.CreateIterator(IteratorType.AutoRefresh);
        iterator.Seek(13);
        zoneTree.Upsert(24, 48);
        for (var i = 17; i < b; ++i)
        {
            if (i != 24 && i % 2 == 0)
                ++i;
            /*
             * New BTree works with forward reading method.
             * This means inserts in the iterator position
             * of BTree Leaf node does not reflect inserts.
             * This is not a bug. Callers can always double check
             * with TryGetKey() if they want to read most recent values
             * for every key they read from iteration.
             * Auto refresh property was made for MutableSegmentMoveForward
             * event. A manual refresh also works but it is expensive to call
             * for every key.
             */
            if (i == 23)
                iterator.Refresh();

            iterator.Next();
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
        }

        Assert.That(iterator.Next(), Is.False);
        Assert.That(iterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b / 2 - 2));
        zoneTree.Maintenance.SaveMetaData();
        iterator.Dispose();
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void IntIntIteratorParallelInserts()
    {
        var dataPath = "data/IntIntIteratorParallelInserts";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var random = new Random();
        var insertCount = 100000;
        var iteratorCount = 1000;

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetMutableSegmentMaxItemCount(insertCount * 2)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .OpenOrCreate();

        var task = Task.Factory.StartNew(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next();
                zoneTree.Upsert(key, key + key);
            });
        });
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = zoneTree.Maintenance.InMemoryRecordCount;
            using var iterator = zoneTree.CreateIterator(IteratorType.NoRefresh);
            iterator.SeekFirst();
            var counter = 0;
            var isValidData = true;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });

        task.Wait();
        zoneTree.Maintenance.DestroyTree();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void IntIntReverseIteratorParallelInserts(bool reverse)
    {
        var dataPath = "data/IntIntReverseIteratorParallelInserts";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var random = new Random();
        var insertCount = 1000000;
        var iteratorCount = 1000;

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetMutableSegmentMaxItemCount(insertCount * 2)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .Configure(x =>
            {
                x.BTreeLockMode = BTreeLockMode.NodeLevelMonitor;
            })
            .OpenOrCreate();

        var task = Task.Run(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next(0, 100_000);
                zoneTree.Upsert(key, key + key);
            });
        });
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = zoneTree.Maintenance.MutableSegmentRecordCount;
            using var iterator =
                reverse ?
                zoneTree.CreateReverseIterator(IteratorType.NoRefresh) :
                zoneTree.CreateIterator(IteratorType.NoRefresh);
            iterator.SeekFirst();
            var counter = 0;
            var isValidData = true;
            var previousKey = reverse ? int.MaxValue : int.MinValue;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                if (reverse && iterator.CurrentKey >= previousKey)
                    throw new Exception("Reverse Iterator is not iterating in valid order.");

                if (!reverse && iterator.CurrentKey <= previousKey)
                    throw new Exception("Iterator is not iterating in valid order.");

                previousKey = iterator.CurrentKey;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });

        task.Wait();
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void IntIntSnapshotIteratorParallelInserts()
    {
        var dataPath = "data/IntIntSnapshotIteratorParallelInserts";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        var random = new Random();
        var insertCount = 100000;
        var iteratorCount = 1000;

        using var zoneTree = new ZoneTreeFactory<int, int>()
            .SetMutableSegmentMaxItemCount(insertCount * 2)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .OpenOrCreate();

        var task = Task.Factory.StartNew(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                zoneTree.Upsert(x, x + x);
            });
        });
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = zoneTree.Maintenance.InMemoryRecordCount;
            using var iterator = zoneTree.CreateIterator(IteratorType.Snapshot);
            iterator.SeekFirst();
            var counter = 0;
            var isValidData = true;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });

        task.Wait();
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void ReversePrefixSearch()
    {
        var dataPath = "data/ReversePrefixSearch";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<string, int>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = a; i <= b; ++i)
        {
            var prefix = (i / 10).ToString() + "-";
            var key = prefix + i;
            zoneTree.Upsert(key, i);
        }

        using var reverseIterator = zoneTree.CreateReverseIterator();

        reverseIterator.Seek("41-");

        for (var i = 409; i >= 250; --i)
        {
            var prefix = (i / 10).ToString() + "-";
            var key = prefix + i;
            Assert.That(reverseIterator.Next(), Is.True);
            Assert.That(reverseIterator.CurrentKey, Is.EqualTo(key));
            Assert.That(reverseIterator.CurrentValue, Is.EqualTo(i));
        }

        Assert.That(reverseIterator.CurrentKey, Is.EqualTo("25-250"));
        Assert.That(reverseIterator.CurrentValue, Is.EqualTo(250));

        Assert.That(reverseIterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b - a + 1));
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void PrefixSearch()
    {
        var dataPath = "data/PrefixSearch";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<string, int>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .OpenOrCreate();
        var a = 250;
        var b = 500;
        for (var i = a; i <= b; ++i)
        {
            var prefix = (i / 10).ToString() + "-";
            var key = prefix + i;
            zoneTree.Upsert(key, i);
        }

        using var iterator = zoneTree.CreateIterator();

        iterator.Seek("41-");

        for (var i = 410; i <= 500; ++i)
        {
            var prefix = (i / 10).ToString() + "-";
            var key = prefix + i;
            Assert.That(iterator.Next(), Is.True);
            Assert.That(iterator.CurrentKey, Is.EqualTo(key));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i));
        }

        Assert.That(iterator.CurrentKey, Is.EqualTo("50-500"));
        Assert.That(iterator.CurrentValue, Is.EqualTo(500));

        Assert.That(iterator.Next(), Is.False);
        Assert.That(zoneTree.Count(), Is.EqualTo(b - a + 1));
        zoneTree.Maintenance.DestroyTree();
    }

    [Test]
    public void SeekIteratorsAfterMerge()
    {
        var dataPath = "data/SeekIteratorsAfterMerge";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<string, int>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetComparer(new StringCurrentCultureComparerAscending())
            .SetKeySerializer(new Utf8StringSerializer())
            .SetValueSerializer(new Int32Serializer())
            .ConfigureDiskSegmentOptions(x =>
            {
                x.DiskSegmentMode = Options.DiskSegmentMode.MultiPartDiskSegment;
                x.MinimumRecordCount = 3;
                x.MaximumRecordCount = 7;
            })
            .OpenOrCreate();
        var n = 500;
        var list = new List<Tuple<string, int>>();
        for (var i = 0; i < n; ++i)
        {
            var key = $"myprefix1|string{i}";
            zoneTree.Upsert(key, i);
            list.Add(Tuple.Create(key, i));

        }
        for (var i = 0; i < n; ++i)
        {
            var key = $"myprefix2|string{i}";
            zoneTree.Upsert(key, i);
            list.Add(Tuple.Create(key, i));
        }
        for (var i = 0; i < n; ++i)
        {
            var key = $"myprefix3|string{i}";
            zoneTree.Upsert(key, i);
            list.Add(Tuple.Create(key, i));
        }
        for (var i = 0; i < n; ++i)
        {
            var key = $"myprefix4|string{i}";
            zoneTree.Upsert(key, i);
            list.Add(Tuple.Create(key, i));
        }
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.CurrentCulture));

        zoneTree.Maintenance.MoveMutableSegmentForward();
        zoneTree.Maintenance.StartMergeOperation()?.Join();

        {
            using var iterator = zoneTree.CreateIterator();
            var prefix = "myprefix2";
            var listIndex = list.FindIndex(x => x.Item1.StartsWith(prefix));
            iterator.Seek(prefix);
            while (iterator.Next())
            {
                var key = iterator.CurrentKey;
                var value = iterator.CurrentValue;
                (var expectedKey, var expectedValue) = list[listIndex++];
                Assert.That(key, Is.EqualTo(expectedKey));
                Assert.That(value, Is.EqualTo(expectedValue));
            }
            Assert.That(listIndex, Is.EqualTo(list.Count));

            using var revIterator = zoneTree.CreateReverseIterator();
            listIndex = list.FindIndex(x => x.Item1.StartsWith(prefix)) - 1;
            revIterator.Seek(prefix);
            while (revIterator.Next())
            {
                var key = revIterator.CurrentKey;
                var value = revIterator.CurrentValue;
                (var expectedKey, var expectedValue) = list[listIndex--];
                Assert.That(key, Is.EqualTo(expectedKey));
                Assert.That(value, Is.EqualTo(expectedValue));
            }
            Assert.That(listIndex, Is.EqualTo(-1));
        }
        {
            using var iterator = zoneTree.CreateIterator();
            var prefix = "myprefix";
            var listIndex = list.FindIndex(x => x.Item1.StartsWith(prefix));
            iterator.Seek(prefix);
            while (iterator.Next())
            {
                var key = iterator.CurrentKey;
                var value = iterator.CurrentValue;
                (var expectedKey, var expectedValue) = list[listIndex++];
                Assert.That(key, Is.EqualTo(expectedKey));
                Assert.That(value, Is.EqualTo(expectedValue));
            }
            Assert.That(listIndex, Is.EqualTo(list.Count));

            using var iterator2 = zoneTree.CreateIterator();
            prefix = "myprefix1";
            listIndex = list.FindIndex(x => x.Item1.StartsWith(prefix));
            iterator2.Seek(prefix);
            while (iterator2.Next())
            {
                var key = iterator2.CurrentKey;
                var value = iterator2.CurrentValue;
                (var expectedKey, var expectedValue) = list[listIndex++];
                Assert.That(key, Is.EqualTo(expectedKey));
                Assert.That(value, Is.EqualTo(expectedValue));
            }
            Assert.That(listIndex, Is.EqualTo(list.Count));

            zoneTree.Maintenance.DestroyTree();

        }
    }
}
