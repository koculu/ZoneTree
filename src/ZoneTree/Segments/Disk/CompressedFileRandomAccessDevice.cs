using System.Collections.Concurrent;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class CompressedFileRandomAccessDevice : IRandomAccessDevice
{
    readonly int BlockSize;

    readonly string Category;

    FileStream FileStream;

    readonly IRandomAccessDeviceManager RandomDeviceManager;

    readonly ConcurrentDictionary<int, DecompressedBlock> DecompressedBlocks = new();

    readonly List<long> CompressedBlockPositions = new();
    
    readonly List<int> CompressedBlockLengths = new();

    int NextBlockIndex = 0;

    int LastBlockLength = 0;

    public string FilePath { get; }

    public int SegmentId { get; }

    public bool Writable { get; }

    public long Length => GetLength();

    public int ReadBufferCount => DecompressedBlocks.Count;

    public CompressedFileRandomAccessDevice(
        int segmentId,
        string category,
        IRandomAccessDeviceManager randomDeviceManager,
        string filePath, 
        bool writable, 
        int compressionBlockSize,
        int fileIOBufferSize = 4096)
    {
        SegmentId = segmentId;
        Category = category;
        RandomDeviceManager = randomDeviceManager;
        FilePath = filePath;
        Writable = writable;
        BlockSize = compressionBlockSize;
        var fileMode = writable ? FileMode.OpenOrCreate : FileMode.Open;
        var fileAccess = writable ? FileAccess.ReadWrite : FileAccess.Read;
        var fileShare = writable ? FileShare.None : FileShare.Read;
        FileStream = new FileStream(filePath,
            fileMode,
            fileAccess,
            fileShare, fileIOBufferSize, false);
        if (FileStream.Length > 0)
        {
            BlockSize = ReadBlockSize();
            (CompressedBlockPositions, CompressedBlockLengths) =
                ReadCompressedBlockPositionsAndLengths();
            NextBlockIndex = CompressedBlockPositions.Count - 1;
            if (NextBlockIndex == -1)
                NextBlockIndex = 0;
            else
            {
                LastBlockLength = ReadBlock(NextBlockIndex).Length;
            }
        }
        else
        {
            WriteBlockSize();
        }
    }

    int ReadBlockSize()
    {
        FileStream.Position = 0;
        var br = new BinaryReader(FileStream);
        return br.ReadInt32();
    }

    void WriteBlockSize()
    {
        FileStream.Position = 0;
        var wr = new BinaryWriter(FileStream);
        wr.Write(BlockSize);
        wr.Flush();
        return;
    }

    long GetLength()
    {
        var length = NextBlockIndex * (long)BlockSize + LastBlockLength;
        return length;
    }

    public long AppendBytesReturnPosition(byte[] bytes)
    {
        var pos = GetLength();
        var len = bytes.Length;
        var copyLen = 0;
        while(copyLen < len)
        {
            copyLen += AppendBytesInternal(bytes.AsSpan(copyLen));
        }
        return pos;
    }

    /// <summary>
    /// Appends the bytes into the block and returns the appended length.
    /// Appends the bytes that fits into the block.
    /// </summary>
    /// <param name="bytes">Bytes</param>
    /// <returns>Appended bytes length.</returns>
    int AppendBytesInternal(ReadOnlySpan<byte> bytes)
    {
        if (!DecompressedBlocks.TryGetValue(NextBlockIndex, out var nextBlock))
        {
            nextBlock = new DecompressedBlock(NextBlockIndex, BlockSize);
            DecompressedBlocks.TryAdd(NextBlockIndex, nextBlock);
            DecompressedBlocks.TryRemove(NextBlockIndex - 1, out _);
        }
        var copyLength = nextBlock.Append(bytes);
        LastBlockLength = nextBlock.Length;
        if (nextBlock.IsFull)
        {
            AppendBlock(nextBlock);
        }
        return copyLength;
    }

    private void AppendBlock(DecompressedBlock nextBlock)
    {
        var compressedBytes = nextBlock.Compress();
        var offset = CompressedBlockPositions.LastOrDefault(sizeof(int));
        offset += CompressedBlockLengths.LastOrDefault();
        FileStream.Position = offset;
        CompressedBlockPositions.Add(offset);
        CompressedBlockLengths.Add(compressedBytes.Length);
        FileStream.Write(compressedBytes);
        FileStream.Flush(true);
        DecompressedBlocks.TryRemove(nextBlock.BlockIndex, out _);
        ++NextBlockIndex;
        LastBlockLength = 0;
    }

    public byte[] GetBytes(long offset, int length)
    {
        var blockIndex = (int)(offset / BlockSize);
        var offsetInBlock = (int)offset % BlockSize;
        if (offsetInBlock + length > BlockSize)
        {
            var chunk1Len = BlockSize - offsetInBlock;
            var chunk2Len = length - chunk1Len;
            var chunk1 = GetBytes(offset, chunk1Len);
            if (chunk1.Length < chunk1Len)
                return chunk1;
            var chunk2 = GetBytes(offset + chunk1Len, chunk2Len);
            var bytes = new byte[chunk1.Length + chunk2.Length];
            Array.Copy(chunk1, bytes, chunk1.Length);
            Array.Copy(chunk2, 0, bytes, chunk1.Length, chunk2.Length);
            return bytes;

        }
        if (!DecompressedBlocks.TryGetValue(blockIndex, out var block))
        {
            if (blockIndex >= CompressedBlockPositions.Count)
                return Array.Empty<byte>();

            return ReadBlockAndAddToCache(blockIndex, offsetInBlock, length);
        }
        block.LastAccessTicks = DateTime.UtcNow.Ticks;
        return block.GetBytes(offsetInBlock, length);
    }

    byte[] ReadBlockAndAddToCache(
        int blockIndex,
        int offsetInBlock,
        int length)
    {
        DecompressedBlock decompressedBlock = ReadBlock(blockIndex);
        DecompressedBlocks.TryAdd(blockIndex, decompressedBlock);
        return decompressedBlock.GetBytes(offsetInBlock, length);
    }

    DecompressedBlock ReadBlock(int blockIndex)
    {
        var blockPositionInFile = CompressedBlockPositions[blockIndex];
        byte[] compressedBytes = null;
        lock (this)
        {
            FileStream.Position = blockPositionInFile;
            var br = new BinaryReader(FileStream);
            var compressedLength = CompressedBlockLengths[blockIndex];
            compressedBytes = br.ReadBytes(compressedLength);
        }
        var decompressedBlock = DecompressedBlock.FromCompressed(blockIndex, compressedBytes);
        decompressedBlock.LastAccessTicks = DateTime.UtcNow.Ticks; 
        return decompressedBlock;
    }

    public int GetBytes(long offset, byte[] buffer)
    {
        var bytes = GetBytes(offset, buffer.Length);
        Array.Copy(bytes, 0, buffer, 0, bytes.Length);
        return bytes.Length;
    }

    public void Dispose()
    {
        if (FileStream == null)
            return;
        Close();
    }

    public void Close()
    {
        DecompressedBlocks.Clear();
        if (FileStream == null)
            return;
        FileStream.Flush(true);
        FileStream.Dispose();
        FileStream = null;
        if (Writable)
            RandomDeviceManager.RemoveWritableDevice(SegmentId, Category);
        else
            RandomDeviceManager.RemoveReadOnlyDevice(SegmentId, Category);
    }

    public void Delete()
    {
        DecompressedBlocks.Clear();
        Dispose();
        File.Delete(FilePath);
    }

    public void ClearContent()
    {
        // first 4 bytes hold the block size.
        FileStream.SetLength(sizeof(int));
        FileStream.Seek(sizeof(int), SeekOrigin.Begin);

        NextBlockIndex = 0;
        CompressedBlockPositions.Clear();
        CompressedBlockLengths.Clear();
        LastBlockLength = 0;
        DecompressedBlocks.Clear();
    }

    public void SealDevice()
    {
        if (DecompressedBlocks.TryGetValue(NextBlockIndex, out var nextBlock))
        {
            AppendBlock(nextBlock);
        }
        WriteCompressedBlockPositionsAndLengths();
    }

    void WriteCompressedBlockPositionsAndLengths()
    {
        var offset = CompressedBlockPositions.LastOrDefault(sizeof(int));
        offset += CompressedBlockLengths.LastOrDefault();
        FileStream.SetLength(offset);
        FileStream.Position = offset;
        var positions = CompressedBlockPositions;
        var lengths = CompressedBlockLengths;
        var len = positions.Count;
        var bw = new BinaryWriter(FileStream);
        for(var i = 0; i < len; ++i)
        {
            bw.Write(positions[i]);
            bw.Write(lengths[i]);
        }
        bw.Write(positions.Count);
        bw.Write(offset);
        FileStream.Flush(true);
    }

    (List<long> positions, List<int> lengths) ReadCompressedBlockPositionsAndLengths()
    {
        FileStream.Seek(-sizeof(int) - sizeof(long), SeekOrigin.End);
        var br = new BinaryReader(FileStream);
        var len = br.ReadInt32();
        var offset = br.ReadInt64();
        FileStream.Seek(offset, SeekOrigin.Begin);
        var positions = new List<long>(len);
        var lengths = new List<int>(len);
        for (var i = 0; i < len; ++i)
        {
            positions.Add(br.ReadInt64());
            lengths.Add(br.ReadInt32());
        }
        return (positions, lengths);
    }

    public int ReleaseReadBuffers(long ticks)
    {
        var removed = 0;
        var blocks = DecompressedBlocks.ToArray();
        var len = blocks.Length;
        for (int i = 0; i < len; i++)
        {
            var b = blocks[i];
            if (b.Value.LastAccessTicks > ticks)
                continue;
            DecompressedBlocks.TryRemove(b.Key, out _);
            ++removed;
        }
        return removed;
    }
}
