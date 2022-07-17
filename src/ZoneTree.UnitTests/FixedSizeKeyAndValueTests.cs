using Tenray;
using Tenray.WAL;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public class FixedSizeKeyAndValueTests
{
    [Test]
    public void IntIntTreeTest()
    {
        var dataPath = "data/IntIntTreeTest";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetMutableSegmentMaxItemCount(5)
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
        for (var i = 0; i < 2000; ++i)
        {
            var result = data.TryGet(i, out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo(i + i));
            Assert.That(data.ContainsKey(i), Is.True);
        }
        data.Maintenance.DestroyTree();
    }

    [Test]
    public void IntStringTreeTest()
    {
        var dataPath = "data/IntStringTreeTest";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<int, string>()
            .SetComparer(new Int32ComparerAscending())
            .SetMutableSegmentMaxItemCount(5)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new UnicodeStringSerializer())
            .OpenOrCreate();
        int n = 2000;
        for (var i = 0; i < n; ++i)
        {
            data.Upsert(i, (i + i).ToString());
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().AsTask().Wait();
        for (var i = 0; i < n; ++i)
        {
            var result = data.TryGet(i, out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo((i + i).ToString()));
            Assert.That(data.ContainsKey(i), Is.True);
        }
        Assert.That(data.TryGet(n + 1, out var _), Is.False);
        data.Maintenance.DestroyTree();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StringIntTreeTest(bool useSparseArray)
    {
        var dataPath = "data/StringIntTreeTest." + useSparseArray;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<string, int>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetMutableSegmentMaxItemCount(5)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new UnicodeStringSerializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        int n = 2000;
        for (var i = 0; i < n; ++i)
        {
            data.Upsert(i.ToString(), i + i);
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().AsTask().Wait();
        if (useSparseArray)
            data.Maintenance.DiskSegment.InitSparseArray(200);
        for (var i = 0; i < n; ++i)
        {
            var result = data.TryGet(i.ToString(), out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo(i + i));
            Assert.That(data.ContainsKey(i.ToString()), Is.True);
        }
        Assert.That(data.TryGet(n+1.ToString(), out var _), Is.False);
        data.Maintenance.DestroyTree();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void StringStringTreeTest(bool useSparseArray)
    {
        var dataPath = "data/StringStringTreeTest." + useSparseArray;
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var data = new ZoneTreeFactory<string, string>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetMutableSegmentMaxItemCount(5)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new UnicodeStringSerializer())
            .SetValueSerializer(new UnicodeStringSerializer())
            .OpenOrCreate();
        int n = 2000;
        for (var i = 0; i < n; ++i)
        {
            data.Upsert(i.ToString(), (i + i).ToString());
        }
        data.Maintenance.MoveSegmentZeroForward();
        data.Maintenance.StartMergeOperation().AsTask().Wait();
        if (useSparseArray)
            data.Maintenance.DiskSegment.InitSparseArray(200);
        for (var i = 0; i < n; ++i)
        {
            var result = data.TryGet(i.ToString(), out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo((i + i).ToString()));
            Assert.That(data.ContainsKey(i.ToString()), Is.True);
        }
        Assert.That(data.TryGet(n + 1.ToString(), out var _), Is.False);
        data.Maintenance.DestroyTree();
    }

    private void ReloadIntIntTreeTestHelper(string dataPath, bool destroy)
    {
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerAscending())
            .SetMutableSegmentMaxItemCount(5)
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .SetIsValueDeletedDelegate((in int x) => x == -1)
            .SetMarkValueDeletedDelegate((ref int x) => x = -1)
            .OpenOrCreate();
        var n = 2000;
        var deleted = new HashSet<int>() { 11, 99, 273, 200, 333, 441, 203, 499, 666}; 
        for (var i = 0; i < n; ++i)
        {
            data.Upsert(i, i + i);
            if (i == 500)
                data.Maintenance.StartMergeOperation().AsTask().Wait();
        }
        foreach (var del in deleted) data.ForceDelete(del);

        for (var i = 0; i < n; ++i)
        {
            if (deleted.Contains(i))
            {
                var result = data.TryGet(i, out var v);
                Assert.That(result, Is.False);
            }
            else
            {
                var result = data.TryGet(i, out var v);
                Assert.That(result, Is.True);
                Assert.That(v, Is.EqualTo(i + i));
                Assert.That(data.ContainsKey(i), Is.True);
            }
        }
        Assert.That(data.Count(), Is.EqualTo(n - deleted.Count));
        if (destroy)
            data.Maintenance.DestroyTree();
    }
    [Test]
    public void ReloadIntIntTreeTest()
    {
        var dataPath = "data/ReloadIntIntTreeTest";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        for (var i = 0; i < 10; ++i)
        {
            ReloadIntIntTreeTestHelper(dataPath, false);
        }
        ReloadIntIntTreeTestHelper(dataPath, true);

    }
}