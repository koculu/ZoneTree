using System.Collections.Concurrent;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class CompressedFileRandomAccessDevice : IRandomAccessDevice
{
    readonly int BlockSize = 32 * 1024;

    readonly string Category;

    FileStream FileStream;

    readonly IRandomAccessDeviceManager RandomDeviceManager;

    readonly ConcurrentDictionary<int, DecompressedBlock> DecompressedBlocks = new();

    readonly List<long> CompressedBlockPositions = new();

    int NextBlockIndex = 0;

    int LastBlockLength = 0;

    public string FilePath { get; }

    public int SegmentId { get; }

    public bool Writable { get; }

    public long Length => GetLength();

    public CompressedFileRandomAccessDevice(
        int segmentId,
        string category,
        IRandomAccessDeviceManager randomDeviceManager,
        string filePath, bool writable, int fileIOBufferSize = 4096)
    {
        SegmentId = segmentId;
        Category = category;
        RandomDeviceManager = randomDeviceManager;
        FilePath = filePath;
        Writable = writable;
        var fileMode = writable ? FileMode.OpenOrCreate : FileMode.Open;
        var fileAccess = writable ? FileAccess.ReadWrite : FileAccess.Read;
        var fileShare = writable ? FileShare.None : FileShare.Read;
        FileStream = new FileStream(filePath,
            fileMode,
            fileAccess,
            fileShare, fileIOBufferSize, false);
        if (FileStream.Length > 0)
        {
            CompressedBlockPositions = ReadCompressedBlockPositions();
            NextBlockIndex = CompressedBlockPositions.Count - 1;
            ReadLastBlockLength();
        }
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
        CompressedBlockPositions.Add(FileStream.Position);
        FileStream.Write(BitConverter.GetBytes(compressedBytes.Length));
        FileStream.Write(compressedBytes);
        FileStream.Flush();
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

            return ReadBlock(length, blockIndex, offsetInBlock);
        }
        block.LastAccessTicks = DateTime.UtcNow.Ticks;
        return block.GetBytes(offsetInBlock, length);
    }

    byte[] ReadBlock(
        int length,
        int blockIndex,
        int offsetInBlock)
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
            var compressedLength = br.ReadInt32();
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
        if (FileStream == null)
            return;
        FileStream.Flush();
        FileStream.Dispose();
        FileStream = null;
        if (Writable)
            RandomDeviceManager.RemoveWritableDevice(SegmentId, Category);
        else
            RandomDeviceManager.RemoveReadOnlyDevice(SegmentId, Category);
    }

    public void Delete()
    {
        Dispose();
        File.Delete(FilePath);
    }

    public void ClearContent()
    {
        FileStream.SetLength(0);
        FileStream.Seek(0, SeekOrigin.Begin);
        NextBlockIndex = 0;
        CompressedBlockPositions.Clear();
        LastBlockLength = 0;
        DecompressedBlocks.Clear();
    }

    public void SealDevice()
    {
        if (DecompressedBlocks.TryGetValue(NextBlockIndex, out var nextBlock))
        {
            AppendBlock(nextBlock);
        }
        WriteCompressedBlockPositions();
    }

    void WriteCompressedBlockPositions()
    {
        var compressedPosition = FileStream.Position;
        var list = CompressedBlockPositions;
        var len = list.Count;
        var bw = new BinaryWriter(FileStream);
        for(var i = 0; i < len; ++i)
        {
            bw.Write(CompressedBlockPositions[i]);
        }
        bw.Write(CompressedBlockPositions.Count);
        bw.Write(compressedPosition);
        FileStream.Flush();
    }

    List<long> ReadCompressedBlockPositions()
    {
        FileStream.Seek(-sizeof(int) - sizeof(long), SeekOrigin.End);
        var br = new BinaryReader(FileStream);
        var len = br.ReadInt32();
        var compressedPosition = br.ReadInt64();
        FileStream.Seek(compressedPosition, SeekOrigin.Begin);
        var list = new List<long>(len);
        for (var i = 0; i < len; ++i)
        {
            list.Add(br.ReadInt64());
        }
        return list;
    }

    void ReadLastBlockLength()
    {
        var list = CompressedBlockPositions;
        var len = list.Count;
        if (len == 0)
        {
            LastBlockLength = 0;
            return;
        }
        var lastBlock = ReadBlock(len - 1);
        LastBlockLength = lastBlock.Length;        
    }

    public int ReleaseReadBuffers(long ticks)
    {
        var removed = 0;
        var blocks = DecompressedBlocks.ToArray();
        foreach (var b in blocks)
        {
            if (b.Value.LastAccessTicks > ticks)
                continue;
            DecompressedBlocks.TryRemove(b.Key, out _);
            ++removed;
        }
        return removed;
    }
}
