using ZoneTree.AbstractFileStream;
using ZoneTree.Collections;
using ZoneTree.Comparers;
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
    public void WalReportsMaximumOpIndexWithoutSorting()
    {
        var filePath = "./WalReportsMaximumOpIndexWithoutSorting.wal";
        if (File.Exists(filePath))
            File.Delete(filePath);
        var serializer = new UnicodeStringSerializer();
        var wal = new SyncFileSystemWriteAheadLog<string, string>(
            new ConsoleLogger(),
            new LocalFileStreamProvider(),
            serializer, serializer, filePath);

        wal.Append("key0", "value0", 5);
        wal.Append("key1", "value1", 2);
        wal.Append("key2", "value2", 7);

        var result = wal.ReadLogEntries(false, false, false);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Keys, Is.EqualTo(new[] { "key0", "key1", "key2" }));
        Assert.That(result.Values, Is.EqualTo(new[] { "value0", "value1", "value2" }));
        Assert.That(result.MaximumOpIndex, Is.EqualTo(7));
        wal.Drop();
    }

    [Test]
    public void DictionaryWithWalKeepsOpIndexAfterReload()
    {
        const string category = "DictionaryWithWalKeepsOpIndexAfterReload";
        var provider = new InMemoryFileStreamProvider();
        var walProvider = new WriteAheadLogProvider(new ConsoleLogger(), provider);
        walProvider.InitCategory(category);
        var options = new WriteAheadLogOptions
        {
            WriteAheadLogMode = WriteAheadLogMode.Sync
        };

        using (var dictionary = CreateDictionaryWithWal(
            walProvider,
            options,
            category))
        {
            dictionary.Upsert(1, "old");
        }

        walProvider = new WriteAheadLogProvider(new ConsoleLogger(), provider);
        walProvider.InitCategory(category);
        using (var dictionary = CreateDictionaryWithWal(
            walProvider,
            options,
            category))
        {
            dictionary.Upsert(1, "new");
        }

        walProvider = new WriteAheadLogProvider(new ConsoleLogger(), provider);
        walProvider.InitCategory(category);
        using (var dictionary = CreateDictionaryWithWal(
            walProvider,
            options,
            category))
        {
            Assert.That(dictionary.TryGetValue(1, out var value), Is.True);
            Assert.That(value, Is.EqualTo("new"));
            dictionary.Drop();
        }
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

    [Test]
    public void CompressedFileStreamSkipAcrossFullBlockIntoShortTail()
    {
        var provider = new InMemoryFileStreamProvider();
        var filePath = "CompressedFileStreamSkipAcrossFullBlockIntoShortTail.wal";
        const int blockSize = 16;
        var block0 = Enumerable.Range(0, blockSize).Select(x => (byte)x).ToArray();
        var tailBlock = Enumerable.Range(blockSize, 8).Select(x => (byte)x).ToArray();

        WriteFile(provider, filePath, CreateMainFileWithSingleBlock(block0));
        WriteFile(provider, filePath + ".tail", CreateTailFile(1, tailBlock));

        var buffer = new byte[1];
        int readLength;
        long seekPosition;
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
            stream.Seek(blockSize - 2, SeekOrigin.Begin);
            seekPosition = stream.Seek(4, SeekOrigin.Current);
            readLength = stream.Read(buffer, 0, buffer.Length);
        }

        Assert.That(seekPosition, Is.EqualTo(blockSize + 2));
        Assert.That(readLength, Is.EqualTo(1));
        Assert.That(buffer[0], Is.EqualTo(tailBlock[2]));
    }

    [Test]
    public void CompressedFileStreamContentIncludingTailCanBeReopened()
    {
        var provider = new InMemoryFileStreamProvider();
        var sourcePath = "CompressedFileStreamContentIncludingTailCanBeReopened.source.wal";
        var restoredPath = "CompressedFileStreamContentIncludingTailCanBeReopened.restored.wal";
        const int blockSize = 16;
        var expected = Enumerable.Range(0, blockSize + 5)
            .Select(x => (byte)x)
            .ToArray();

        byte[] exportedBytes;
        using (var source = new CompressedFileStream(
            new ConsoleLogger(),
            provider,
            sourcePath,
            blockSize,
            false,
            0,
            CompressionMethod.None,
            0))
        {
            source.Write(expected, 0, expected.Length);
            exportedBytes = source.GetFileContentIncludingTail();
        }

        WriteFile(provider, restoredPath, exportedBytes);

        var actual = new byte[expected.Length];
        int readLength;
        long restoredLength;
        using (var restored = new CompressedFileStream(
            new ConsoleLogger(),
            provider,
            restoredPath,
            blockSize,
            false,
            0,
            CompressionMethod.None,
            0))
        {
            restored.Seek(0, SeekOrigin.Begin);
            readLength = restored.Read(actual, 0, actual.Length);
            restoredLength = restored.Length;
        }

        Assert.That(exportedBytes[0], Is.EqualTo((byte)CompressionMethod.None));
        Assert.That(restoredLength, Is.EqualTo(expected.Length));
        Assert.That(readLength, Is.EqualTo(expected.Length));
        Assert.That(actual, Is.EqualTo(expected));
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

    static byte[] CreateMainFileWithSingleBlock(byte[] block)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)CompressionMethod.None);
        WriteCompressedBlock(writer, 0, block, block.Length);
        writer.Flush();
        return stream.ToArray();
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

    static DictionaryWithWAL<int, string> CreateDictionaryWithWal(
        WriteAheadLogProvider walProvider,
        WriteAheadLogOptions options,
        string category)
    {
        return new DictionaryWithWAL<int, string>(
            0,
            category,
            walProvider,
            options,
            new Int32Serializer(),
            new UnicodeStringSerializer(),
            new Int32ComparerAscending(),
            IsDeletedString,
            MarkStringDeleted);
    }

    static bool IsDeletedString(in int key, in string value)
    {
        return value == null;
    }

    static void MarkStringDeleted(ref string value)
    {
        value = null;
    }
}
