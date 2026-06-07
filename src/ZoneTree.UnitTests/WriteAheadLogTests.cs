using ZoneTree.AbstractFileStream;
using ZoneTree.Core;
using ZoneTree.Exceptions;
using ZoneTree.Logger;
using ZoneTree.Options;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public sealed class WriteAheadLogTests
{
    [Test]
    public void WalBasicTest()
    {
        var filePath = "./WalBasicTest.wal";
        if (File.Exists(filePath))
            File.Delete(filePath);
        var serializer = new UnicodeStringSerializer();
        var wal = new SyncFileSystemWriteAheadLog<string, string>(
            new ConsoleLogger(),
            new LocalFileStreamProvider(),
            serializer, serializer, filePath);
        var len = 1;
        for (var i = 0; i < len; ++i)
        {
            wal.Append("key" + i, "value" + i, i);
        }
        var result = wal.ReadLogEntries(false, false, true);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Exceptions, Is.Empty);
        Assert.That(result.Keys.Count, Is.EqualTo(len));
        Assert.That(result.Values.Count, Is.EqualTo(len));
        for (var i = 0; i < len; ++i)
        {
            Assert.That(result.Keys[i], Is.EqualTo("key" + i));
            Assert.That(result.Values[i], Is.EqualTo("value" + i));                
        }
        Assert.That(result.MaximumOpIndex, Is.EqualTo(len-1));
        wal.Drop();
    }

    [Test]
    public void TestWriteAheadLogCorruptionException()
    {
        Assert.DoesNotThrow(() => { new WriteAheadLogCorruptionException(0, null); });
        Assert.DoesNotThrow(() => { new WriteAheadLogCorruptionException(0, 
            new Dictionary<int, Exception> { }); });
    }

    [Test]
    public void CompressedFileStreamIgnoresIncompleteMainBlockPayload()
    {
        var provider = new InMemoryFileStreamProvider();
        var filePath = "CompressedFileStreamIgnoresIncompleteMainBlockPayload.wal";
        const int blockSize = 16;
        var block0 = Enumerable.Range(0, blockSize).Select(x => (byte)x).ToArray();
        var block1 = Enumerable.Range(blockSize, blockSize).Select(x => (byte)x).ToArray();

        WriteFile(provider, filePath, CreateMainFileWithIncompleteSecondBlock(block0, block1, 4));
        WriteFile(provider, filePath + ".tail", CreateTailFile(1, block1));

        var expected = block0.Concat(block1).ToArray();
        var actual = new byte[expected.Length];
        long streamLength;
        int readLength;
        using (var stream = new CompressedFileStream(
            new ConsoleLogger(),
            provider,
            filePath,
            blockSize,
            false,
            0,
            CompressionMethod.None,
            0))
        {
            stream.Seek(0, SeekOrigin.Begin);
            readLength = stream.Read(actual, 0, actual.Length);
            streamLength = stream.Length;
        }

        Assert.That(streamLength, Is.EqualTo(expected.Length));
        Assert.That(readLength, Is.EqualTo(expected.Length));
        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(
            provider.ReadAllBytes(filePath).Length,
            Is.EqualTo(sizeof(byte) + 3 * sizeof(int) + block0.Length));
    }

    static void WriteFile(
        InMemoryFileStreamProvider provider,
        string path,
        byte[] bytes)
    {
        using var stream = provider.CreateFileStream(
            path,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
    }

    static byte[] CreateMainFileWithIncompleteSecondBlock(
        byte[] block0,
        byte[] block1,
        int secondBlockBytesWritten)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)CompressionMethod.None);
        WriteCompressedBlock(writer, 0, block0, block0.Length);
        WriteCompressedBlock(writer, 1, block1, secondBlockBytesWritten);
        writer.Flush();
        return stream.ToArray();
    }

    static byte[] CreateTailFile(int blockIndex, byte[] block)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(blockIndex);
        writer.Write(block.Length);
        writer.Write(block);
        writer.Flush();
        return stream.ToArray();
    }

    static void WriteCompressedBlock(
        BinaryWriter writer,
        int blockIndex,
        byte[] block,
        int bytesToWrite)
    {
        writer.Write(blockIndex);
        writer.Write(block.Length);
        writer.Write(block.Length);
        writer.Write(block, 0, bytesToWrite);
    }
}
