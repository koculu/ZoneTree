using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Collections.BplusTree.Lock;
using Tenray.ZoneTree.Collections.BTree;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.UnitTests;

public class SafeBTreeTests
{
    [TestCase(BTreeLockMode.NoLock)]
    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void BTreeIteration(BTreeLockMode lockMode)
    {
        var n = 2000;
        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), lockMode);
        for (var i = 0; i < n; ++i)
            tree.TryInsert(i, i + i, out _);

        var iterator = new BTreeSeekableIterator<int, int>(tree);
        var j = 0; 
        while (iterator.Next())
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(j));
            Assert.That(iterator.CurrentValue, Is.EqualTo(j + j));
            ++j;
        } 

        iterator.SeekEnd();
        j = n - 1;
        do
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(j));
            Assert.That(iterator.CurrentValue, Is.EqualTo(j + j));
            --j;
        } while (iterator.Prev());

    }

    [TestCase(BTreeLockMode.NoLock)]
    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void SafeBTreeIteration2(BTreeLockMode lockMode)
    {
        var n = 3;
        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), lockMode);
        for (var i = 0; i < n; ++i)
            tree.TryInsert(i, i + i, out _);

        var iterator = new BTreeSeekableIterator<int, int>(tree);
        iterator.SeekEnd();
        for (var i = n - 1; i >= 0; --i)
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
            iterator.Prev();
        }
        for (var i = 0; i < n; ++i)
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(i));
            Assert.That(iterator.CurrentValue, Is.EqualTo(i + i));
            iterator.Next();
        }
        Assert.That(iterator.Prev(), Is.True);
        Assert.That(iterator.Next(), Is.True);
        Assert.That(iterator.Next(), Is.False);
    }

    [TestCase(BTreeLockMode.NoLock)]
    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void BTreeLowerOrEqualBound(BTreeLockMode lockMode)
    {
        int n = 10;
        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), lockMode);
        for (var i = 1; i < n; i += 2)
            tree.TryInsert(i, i, out _);
        var iterator = new BTreeSeekableIterator<int, int>(tree);
        Assert.Multiple(() =>
        {
            // 1 3 5 7 9
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 4), Is.EqualTo(3));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 3), Is.EqualTo(3));
            Assert.Throws<IndexOutOfRangeException>(
                () => GetLastNodeSmallerOrEqual(iterator ,- 1));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 10), Is.EqualTo(9));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 9), Is.EqualTo(9));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 1), Is.EqualTo(1));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 5), Is.EqualTo(5));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 7), Is.EqualTo(7));
            Assert.That(GetLastNodeSmallerOrEqual(iterator, 8), Is.EqualTo(7));
            Assert.Throws<IndexOutOfRangeException>(
                () => GetLastNodeSmallerOrEqual(iterator, 0));

            Assert.That(GetFirstNodeGreaterOrEqual(iterator, -1), Is.EqualTo(1));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 1), Is.EqualTo(1));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 2), Is.EqualTo(3));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 3), Is.EqualTo(3));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 4), Is.EqualTo(5));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 5), Is.EqualTo(5));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 6), Is.EqualTo(7));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 7), Is.EqualTo(7));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 8), Is.EqualTo(9));
            Assert.That(GetFirstNodeGreaterOrEqual(iterator, 9), Is.EqualTo(9));
            Assert.Throws<IndexOutOfRangeException>(
                () => GetFirstNodeGreaterOrEqual(iterator, 10));
        });
    }

    int GetLastNodeSmallerOrEqual(BTreeSeekableIterator<int, int> iterator, int key)
    {
        iterator.SeekToLastSmallerOrEqualElement(in key);
        return iterator.CurrentKey;
    }

    int GetFirstNodeGreaterOrEqual(BTreeSeekableIterator<int, int> iterator, int key)
    {
        iterator.SeekToFirstGreaterOrEqualElement(in key);
        return iterator.CurrentKey;
    }

    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void BTreeIteratorParallelInserts(BTreeLockMode lockMode)
    {
        var random = new Random();
        var insertCount = 100000;
        var iteratorCount = 1000;

        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), lockMode);

        var task = Task.Factory.StartNew(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next();
                tree.AddOrUpdate(key,
                    AddOrUpdateResult (ref int value) =>
                    {
                        value = key + key;
                        return AddOrUpdateResult.ADDED;
                    },
                    AddOrUpdateResult (ref int value) =>
                    {
                        value = key + key;
                        return AddOrUpdateResult.UPDATED;
                    }, out _);
            });
        });
        Thread.Sleep(100);
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = tree.Length;
            var iterator = new BTreeSeekableIterator<int, int>(tree);
            var counter = 0;
            var isValidData = true;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            if (counter < initialCount)
            {
                Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
                Assert.That(isValidData, Is.True);
            }
        });

        task.Wait();
    }

    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void BTreeReverseIteratorParallelInserts(BTreeLockMode lockMode)
    {
        var random = new Random();
        var insertCount = 100000;
        var iteratorCount = 1550;

        var tree = new BTree<int, int>(
            new Int32ComparerAscending(), lockMode);

        var task = Task.Factory.StartNew(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next();
                tree.AddOrUpdate(key,
                    AddOrUpdateResult  (ref int x) =>
                    {
                        x = key + key;
                        return AddOrUpdateResult.ADDED;
                    },
                    AddOrUpdateResult (ref int y) =>
                    {
                        y = key + key;
                        return AddOrUpdateResult.UPDATED;
                    }, out _);
            });
        });
        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = tree.Length;
            var iterator = new BTreeSeekableIterator<int, int>(tree);
            var counter = iterator.SeekEnd() ? 1 : 0;
            var isValidData = true;
            while (iterator.Prev())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount), "prev iterator");
            Assert.That(isValidData, Is.True);

            initialCount = tree.Length;
            counter = iterator.SeekBegin() ? 1 : 0;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount), "next iterator");
            Assert.That(isValidData, Is.True);
        });

        task.Wait();
    }

    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void IntIntDuplicateRecords(BTreeLockMode lockMode)
    {
        var random = new Random();
        var insertCount = 1000000;
        var iteratorCount = 1000;

        var tree = new BTree<int, int>(new Int32ComparerAscending(), lockMode);
        var task = Task.Run(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next(0, 100000);
                tree.Upsert(key, key + key, out _);
            });
        });

        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = tree.Length;
            var iterator = new BTreeSeekableIterator<int, int>(tree);
            var counter = iterator.SeekBegin() ? 1 : 0;
            var isValidData = true;
            var previousKey = int.MinValue;
            while (iterator.Next())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                if (iterator.CurrentKey <= previousKey)
                    throw new Exception(
                        $"Iterator is not iterating in valid order.{iterator.CurrentKey} <= {previousKey}");
                previousKey = iterator.CurrentKey;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });
        task.Wait();
    }

    [TestCase(BTreeLockMode.TopLevelReaderWriter)]
    [TestCase(BTreeLockMode.TopLevelMonitor)]
    [TestCase(BTreeLockMode.NodeLevelReaderWriter)]
    [TestCase(BTreeLockMode.NodeLevelMonitor)]
    public void IntIntDuplicateReverseRecords(BTreeLockMode lockMode)
    {
        var random = new Random();
        var insertCount = 1000000;
        var iteratorCount = 1000;

        var tree = new BTree<int, int>(new Int32ComparerAscending(), lockMode);
        var task = Task.Run(() =>
        {
            Parallel.For(0, insertCount, (x) =>
            {
                var key = random.Next(0, 100000);
                tree.Upsert(key, key + key, out _);
            });
        });

        Parallel.For(0, iteratorCount, (x) =>
        {
            var initialCount = tree.Length;
            var iterator = new BTreeSeekableIterator<int, int>(tree);
            var counter = iterator.SeekEnd() ? 1 : 0;
            var isValidData = true;
            var previousKey = int.MaxValue;
            while (iterator.Prev())
            {
                var expected = iterator.CurrentKey + iterator.CurrentKey;
                if (iterator.CurrentValue != expected)
                    isValidData = false;
                if (iterator.CurrentKey >= previousKey)
                    throw new Exception(
                        $"Reverse Iterator is not iterating in valid order.{iterator.CurrentKey} >= {previousKey}");
                previousKey = iterator.CurrentKey;
                ++counter;
            }
            Assert.That(counter, Is.GreaterThanOrEqualTo(initialCount));
            Assert.That(isValidData, Is.True);
        });
        task.Wait();
    }
}
