using System.Text;
using Tenray.WAL;
using ZoneTree.Serializers;

namespace ZoneTree.UnitTests
{
    public class WriteAheadLogTests
    {
        [Test]
        public void WalBasicTest()
        {
            var filePath = "./WalBasicTest.wal";
            if (File.Exists(filePath))
                File.Delete(filePath);
            var serializer = new UnicodeStringSerializer();
            var wal = new FileSystemWriteAheadLog<string, string>(serializer, serializer, filePath);
            var len = 1;
            for (var i = 0; i  < len;++i)
            {
                wal.Append("key" + i, "value" + i);
            }
            var result = wal.ReadLogEntries(false, false);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Exceptions, Is.Empty);
            Assert.That(result.Keys.Count, Is.EqualTo(len));
            Assert.That(result.Values.Count, Is.EqualTo(len));
            for (var i = 0; i < len; ++i)
            {
                Assert.That(result.Keys[i], Is.EqualTo("key" + i));
                Assert.That(result.Values[i], Is.EqualTo("value" + i));
            }
            wal.Drop();
        }
    }
}