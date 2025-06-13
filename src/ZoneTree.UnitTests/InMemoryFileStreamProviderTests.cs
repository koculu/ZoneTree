using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests;

public sealed class InMemoryFileStreamProviderTests
{
    [Test]
    public void WalWithInMemoryProvider()
    {
        var serializer = new UnicodeStringSerializer();
        var provider = new InMemoryFileStreamProvider();
        var wal = new SyncFileSystemWriteAheadLog<string, string>(
            new ConsoleLogger(),
            provider,
            serializer,
            serializer,
            "test.wal");
        wal.Append("hello", "world", 0);
        var result = wal.ReadLogEntries(false, false, true);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Keys[0], Is.EqualTo("hello"));
        Assert.That(result.Values[0], Is.EqualTo("world"));
        wal.Drop();
    }
}
