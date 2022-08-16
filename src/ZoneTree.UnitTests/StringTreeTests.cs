using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests;

public class StringTreeTests
{
    [Test]
    public void NullStringKeyTest()
    {
        var dataPath = "data/NullStringKeyTest";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<string, string>()
            .SetComparer(new StringOrdinalComparerAscending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Utf8StringSerializer())
            .SetValueSerializer(new Utf8StringSerializer())
            .OpenOrCreate();

        var keys = new[]
        {
            null, "", "Abc", "Cbc", "Dbc", "zWithNullValue"
        };
        var values = new[]
        {
            "Zbc", "DDD", "Abc", "Cbc", "Dbc", null
        };
        for(var i = 0; i < keys.Length; i++)
        {
            zoneTree.Upsert(keys[i], values[i]);
        }

        var iterator = zoneTree.CreateIterator();
        var j = 0;
        while (iterator.Next())
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(keys[j]));
            Assert.That(iterator.CurrentValue, Is.EqualTo(values[j]));
            ++j;
        }
        var maintenance = zoneTree.Maintenance;
        maintenance.MoveSegmentZeroForward();
        maintenance.StartMergeOperation()?.Join();

        iterator = zoneTree.CreateIterator();
        j = 0;
        while (iterator.Next())
        {
            Assert.That(iterator.CurrentKey, Is.EqualTo(keys[j]));
            Assert.That(iterator.CurrentValue, Is.EqualTo(values[j]));
            ++j;
        }
        zoneTree.Maintenance.DestroyTree();
    }
}