using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.UnitTests;

public class TTLTests
{
    public struct TTLValue
    {
        public int Value;

        public DateTime Expiration;

        public TTLValue(int value, DateTime expiration)
        {
            Value = value;
            Expiration = expiration;
        }

        public bool IsExpired()
        {
            return DateTime.UtcNow >= Expiration;
        }

        public void Expire()
        {
            Expiration = new DateTime();
        }

        public void SlideExpiration(TimeSpan timeSpan)
        {
            Expiration = Expiration.Add(timeSpan);
        }
    }

    [Test]
    public void TestTTL()
    {
        var dataPath = "data/TestTTL";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);

        using var zoneTree = new ZoneTreeFactory<int, TTLValue>()
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetValueSerializer(new StructSerializer<TTLValue>())
            .SetIsValueDeletedDelegate((in TTLValue x) => x.IsExpired())
            .SetMarkValueDeletedDelegate(void (ref TTLValue x) => x.Expire())
            .OpenOrCreate();

        zoneTree.Upsert(5, new TTLValue(99, DateTime.UtcNow.AddMilliseconds(300)));
        var f1 = zoneTree.TryGet(5, out var v1);
        Thread.Sleep(300);
        var f2 = zoneTree.TryGet(5, out var v2);

        Assert.That(f1, Is.True);
        Assert.That(f2, Is.False);

        zoneTree.Upsert(5, new TTLValue(99, DateTime.UtcNow.AddMilliseconds(300)));
        Thread.Sleep(150);
        f1 = zoneTree.TryGetAndUpdate(
            5,
            out v1, 
            void (ref TTLValue v) => 
                v.SlideExpiration(TimeSpan.FromMilliseconds(300)));
        Thread.Sleep(300);
        f2 = zoneTree.TryGetAndUpdate(
            5,
            out v2,
            void (ref TTLValue v) =>
                v.SlideExpiration(TimeSpan.FromMilliseconds(300)));

        Assert.That(f1, Is.True);
        Assert.That(f2, Is.False);

        zoneTree.Maintenance.DestroyTree();
    }
}
