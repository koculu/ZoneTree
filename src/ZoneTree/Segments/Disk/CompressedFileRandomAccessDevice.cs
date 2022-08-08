﻿using System.Collections.Concurrent;
using System.Text;
using Tenray.ZoneTree.AbstractFileStream;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class CompressedFileRandomAccessDevice : IRandomAccessDevice
{
    readonly IFileStreamProvider FileStreamProvider;

    readonly int BlockSize;

    readonly string Category;

    IFileStream FileStream;

    readonly BinaryReader BinaryReader;

    readonly BinaryWriter BinaryWriter;

    readonly IRandomAccessDeviceManager RandomDeviceManager;

    readonly ConcurrentDictionary<int, DecompressedBlock> DecompressedBlocks = new();

    readonly List<long> CompressedBlockPositions = new();
    
    readonly List<int> CompressedBlockLengths = new();

    int NextBlockIndex = 0;

    int LastBlockLength = 0;

    public string FilePath { get; }

    public long SegmentId { get; }

    public bool Writable { get; }

    public long Length => GetLength();

    public int ReadBufferCount => DecompressedBlocks.Count;

    readonly int MaxCachedBlockCount;

    public CompressedFileRandomAccessDevice(
        int maxCachedBlockCount,
        IFileStreamProvider fileStreamProvider,
        long segmentId,
        string category,
        IRandomAccessDeviceManager randomDeviceManager,
        string filePath, 
        bool writable, 
        int compressionBlockSize,
        int fileIOBufferSize = 4096)
    {
        MaxCachedBlockCount = maxCachedBlockCount;
        FileStreamProvider = fileStreamProvider;
        SegmentId = segmentId;
        Category = category;
        RandomDeviceManager = randomDeviceManager;
        FilePath = filePath;
        Writable = writable;
        BlockSize = compressionBlockSize;
        var fileMode = writable ? FileMode.OpenOrCreate : FileMode.Open;
        var fileAccess = writable ? FileAccess.ReadWrite : FileAccess.Read;
        var fileShare = writable ? FileShare.None : FileShare.Read;
        FileStream = fileStreamProvider.CreateFileStream(filePath,
            fileMode,
            fileAccess,
            fileShare, fileIOBufferSize, FileOptions.None);
        BinaryReader = new BinaryReader(FileStream.ToStream(), Encoding.UTF8, true);
        if (writable)
            BinaryWriter = new BinaryWriter(FileStream.ToStream(), Encoding.UTF8, true);
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
        return BinaryReader.ReadInt32();
    }

    void WriteBlockSize()
    {
        FileStream.Position = 0;
        BinaryWriter.Write(BlockSize);
        BinaryWriter.Flush();
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

    bool TryGetBlockCache(int blockIndex, out DecompressedBlock block)
    {
        if (!DecompressedBlocks.TryGetValue(blockIndex % MaxCachedBlockCount, out block))
            return false;
        if (block.BlockIndex != blockIndex)
        {
            block = null;
            return false;
        }
        return true;
    }
    /// <summary>
    /// Appends the bytes into the block and returns the appended length.
    /// Appends the bytes that fits into the block.
    /// </summary>
    /// <param name="bytes">Bytes</param>
    /// <returns>Appended bytes length.</returns>
    int AppendBytesInternal(ReadOnlySpan<byte> bytes)
    {
        if (!TryGetBlockCache(NextBlockIndex, out var nextBlock))
        {
            nextBlock = new DecompressedBlock(NextBlockIndex, BlockSize);
            DecompressedBlocks.TryAdd(NextBlockIndex % MaxCachedBlockCount, nextBlock);
            DecompressedBlocks.TryRemove((NextBlockIndex - 1) % MaxCachedBlockCount, out _);
        }
        var copyLength = nextBlock.Append(bytes);
        LastBlockLength = nextBlock.Length;
        if (nextBlock.IsFull)
        {
            AppendBlock(nextBlock);
        }
        return copyLength;
    }

    void AppendBlock(DecompressedBlock nextBlock)
    {
        var compressedBytes = nextBlock.Compress();
        var offset = CompressedBlockPositions.LastOrDefault(sizeof(int));
        offset += CompressedBlockLengths.LastOrDefault();
        FileStream.Position = offset;
        CompressedBlockPositions.Add(offset);
        CompressedBlockLengths.Add(compressedBytes.Length);
        FileStream.Write(compressedBytes);
        FileStream.Flush(true);
        DecompressedBlocks.TryRemove(nextBlock.BlockIndex % MaxCachedBlockCount, out _);
        ++NextBlockIndex;
        LastBlockLength = 0;
    }

    public byte[] GetBytes(long offset, int length)
    {
        var blockIndex = (int)(offset / BlockSize);
        var offsetInBlock = (int)(offset % BlockSize);
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
        if (!TryGetBlockCache(blockIndex, out var block))
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
        DecompressedBlocks.AddOrUpdate(
            blockIndex % MaxCachedBlockCount,
            decompressedBlock,
            (key, value) => decompressedBlock);
        return decompressedBlock.GetBytes(offsetInBlock, length);
    }

    DecompressedBlock ReadBlock(int blockIndex)
    {
        var blockPositionInFile = CompressedBlockPositions[blockIndex];
        byte[] compressedBytes = null;
        lock (this)
        {
            FileStream.Position = blockPositionInFile;
            var compressedLength = CompressedBlockLengths[blockIndex];
            compressedBytes = BinaryReader.ReadBytes(compressedLength);
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
        FileStreamProvider.DeleteFile(FilePath);
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
        if (TryGetBlockCache(NextBlockIndex, out var nextBlock))
        {
            AppendBlock(nextBlock);
            LastBlockLength = nextBlock.Length;
            --NextBlockIndex;
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
        var bw = BinaryWriter;
        for (var i = 0; i < len; ++i)
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
        var br = BinaryReader;
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
            DecompressedBlocks.TryRemove(b.Key % MaxCachedBlockCount, out _);
            ++removed;
        }
        return removed;
    }
}
