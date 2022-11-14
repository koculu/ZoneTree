using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.UnitTests
{
    public class Crc32ComputerTests
    {
        [Test]
        public void Crc32UintUlongTest()
        {
            var result = Crc32Computer.Compute(0, (ulong)123456789);
            Assert.That(result, Is.EqualTo(3531890030));
        }

        [Test]
        public void Crc32UintUintTest()
        {
            var result = Crc32Computer.Compute(0, (uint)123456789);
            Assert.That(result, Is.EqualTo(3177508098));
        }

        [Test]
        public void Crc32UintIntTest()
        {
            var result = Crc32Computer.Compute(0, 123456789);
            Assert.That(result, Is.EqualTo(3177508098));
        }

        [Test]
        [TestCase(new byte[]{0x1, 0x2, 0x3, 0x4, 0x5}, (uint)371456414)]
        [TestCase(new byte[]{0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0x10}, (uint)712557653)]
        public void Crc32UintBytesTest(byte[] data, uint expected)
        {
            var result = Crc32Computer.Compute(0, data);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}