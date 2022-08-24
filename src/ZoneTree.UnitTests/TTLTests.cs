using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.PresetTypes;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.UnitTests;

public class TTLTests
{
    [Test]
    public void TestTTL()
    {
        var dataPath = "data/TestTTL";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, TTLValue<int>>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetValueSerializer(new StructSerializer<TTLValue<int>>())
            .SetIsValueDeletedDelegate((in TTLValue<int> x) => x.IsExpired)
            .SetMarkValueDeletedDelegate(void (ref TTLValue<int> x) => x.Expire())
            .OpenOrCreate();

        zoneTree.Upsert(5, new TTLValue<int>(99, DateTime.UtcNow.AddMilliseconds(300)));
        var f1 = zoneTree.TryGet(5, out var v1);
        Thread.Sleep(300);
        var f2 = zoneTree.TryGet(5, out var v2);

        Assert.That(f1, Is.True);
        Assert.That(f2, Is.False);

        zoneTree.Upsert(5, new TTLValue<int>(99, DateTime.UtcNow.AddMilliseconds(300)));
        Thread.Sleep(150);
        f1 = zoneTree.TryGetAndUpdate(
            5,
            out v1, 
            bool (ref TTLValue<int> v) => 
                v.SlideExpiration(TimeSpan.FromMilliseconds(300)));
        Thread.Sleep(450); // initial expiration (300) + slided expiration (300) - Thread.Sleep(150)
        f2 = zoneTree.TryGetAndUpdate(
            5,
            out v2,
            bool (ref TTLValue<int> v) =>
                v.SlideExpiration(TimeSpan.FromMilliseconds(300)));

        Assert.That(f1, Is.True);
        Assert.That(f2, Is.False);

        zoneTree.Maintenance.DestroyTree();
    }
}
