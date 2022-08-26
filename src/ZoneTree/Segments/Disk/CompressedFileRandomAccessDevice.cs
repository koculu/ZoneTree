using System.Text;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class CompressedFileRandomAccessDevice : IRandomAccessDevice
{
    const int MetaDataSize = 5;

    readonly IFileStreamProvider FileStreamProvider;

    readonly int BlockSize;

    readonly CompressionMethod CompressionMethod;
    
    readonly int CompressionLevel;

    readonly string Category;

    IFileStream FileStream;

    readonly BinaryReader BinaryReader;

    readonly BinaryWriter BinaryWriter;

    readonly IRandomAccessDeviceManager RandomDeviceManager;

    /// <summary>
    /// Why not LRUCache?
    /// Because it is 40% slower. See thread-safe LRUBlockCache implementation.
    /// The collision problem of multiple readers of a circular block cache
    /// can be addressed by increasing BlockCacheLimit.
    /// </summary>
    readonly CircularBlockCache CircularBlockCache;

    readonly List<long> CompressedBlockPositions = new();
    
    readonly List<int> CompressedBlockLengths = new();

    readonly List<int> DecompressedBlockLengths = new();

    readonly object[] BlockReadLocks = new object[33];

    int NextBlockIndex = 0;

    DecompressedBlock NextBlock;

    int LastBlockLength = 0;

    public string FilePath { get; }

    public long SegmentId { get; }

    public bool Writable { get; }

    public long Length => GetLength();

    public int ReadBufferCount => CircularBlockCache.Count;

    public struct CompressedFileMeta
    {
        public int BlockSize;

        public CompressionMethod CompressionMethod;

        public CompressedFileMeta(int blockSize,
            CompressionMethod compressionMethod) : this()
        {
            BlockSize = blockSize;
            CompressionMethod = compressionMethod;
        }
    }

    public CompressedFileRandomAccessDevice(
        ILogger logger,
        int maxCachedBlockCount,
        IFileStreamProvider fileStreamProvider,
        long segmentId,
        string category,
        IRandomAccessDeviceManager randomDeviceManager,
        string filePath,
        bool writable,
        int compressionBlockSize,
        CompressionMethod compressionMethod,
        int compressionLevel,
        long blockCacheReplacementWarningDuration,
        int fileIOBufferSize = 4096)
    {
        for (var i = 0; i < BlockReadLocks.Length; ++i)
        {
            BlockReadLocks[i] = new object();
        }

        CircularBlockCache = new(
            logger, maxCachedBlockCount, blockCacheReplacementWarningDuration);
        FileStreamProvider = fileStreamProvider;
        SegmentId = segmentId;
        Category = category;
        RandomDeviceManager = randomDeviceManager;
        FilePath = filePath;
        Writable = writable;
        BlockSize = compressionBlockSize;
        CompressionMethod = compressionMethod;
        CompressionLevel = compressionLevel;
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
            var meta = ReadMetaData();
            BlockSize = meta.BlockSize;
            CompressionMethod = meta.CompressionMethod;

            (CompressedBlockPositions,
             CompressedBlockLengths, 
             DecompressedBlockLengths) =
                ReadCompressedBlockPositionsAndLengths();
            NextBlockIndex = CompressedBlockPositions.Count - 1;
            if (NextBlockIndex == -1)
                NextBlockIndex = 0;
            else
            {
                LastBlockLength = DecompressedBlockLengths.LastOrDefault();
            }
        }
        else
        {
            WriteMetaData();
        }
    }

    CompressedFileMeta ReadMetaData()
    {
        FileStream.Position = 0;
        return new CompressedFileMeta(
            BinaryReader.ReadInt32(),
            (CompressionMethod)BinaryReader.ReadByte());
    }

    void WriteMetaData()
    {
        FileStream.Position = 0;
        BinaryWriter.Write(BlockSize);
        BinaryWriter.Write((byte)CompressionMethod);
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
        return CircularBlockCache.TryGetBlock(blockIndex, out block);
    }

    /// <summary>
    /// Appends the bytes into the block and returns the appended length.
    /// Appends the bytes that fits into the block.
    /// </summary>
    /// <param name="bytes">Bytes</param>
    /// <returns>Appended bytes length.</returns>
    int AppendBytesInternal(ReadOnlySpan<byte> bytes)
    {
        if (NextBlock == null)
        {
            NextBlock = new DecompressedBlock(
                NextBlockIndex, BlockSize, CompressionMethod, CompressionLevel);
        }
        var nextBlock = NextBlock;
        nextBlock.LastAccessTicks = Environment.TickCount64;

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
        var offset = CompressedBlockPositions.LastOrDefault(MetaDataSize);
        offset += CompressedBlockLengths.LastOrDefault();
        FileStream.Position = offset;
        CompressedBlockPositions.Add(offset);
        CompressedBlockLengths.Add(compressedBytes.Length);
        DecompressedBlockLengths.Add(nextBlock.Length);
        FileStream.Write(compressedBytes);
        FileStream.Flush(true);
        CircularBlockCache.RemoveBlock(nextBlock.BlockIndex);
        ++NextBlockIndex;
        LastBlockLength = 0;
        NextBlock = null;
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
        var block = NextBlock;
        if ((block == null || block.BlockIndex != blockIndex) &&
            !TryGetBlockCache(blockIndex, out block))
        {
            if (blockIndex >= CompressedBlockPositions.Count)
                return Array.Empty<byte>();

            return ReadBlockAndAddToCache(blockIndex, offsetInBlock, length);
        }
        block.LastAccessTicks = Environment.TickCount64;
        return block.GetBytes(offsetInBlock, length);
    }

    byte[] ReadBlockAndAddToCache(
        int blockIndex,
        int offsetInBlock,
        int length)
    {
        var blockLock = BlockReadLocks[blockIndex % BlockReadLocks.Length];
        lock (blockLock)
        {
            var block = NextBlock;
            if ((block == null || block.BlockIndex != blockIndex) &&
                !TryGetBlockCache(blockIndex, out block))
            {
                DecompressedBlock decompressedBlock = ReadBlock(blockIndex);
                CircularBlockCache.AddBlock(decompressedBlock);
                NextBlock = decompressedBlock;
                return decompressedBlock.GetBytes(offsetInBlock, length);
            }
            block.LastAccessTicks = Environment.TickCount64;
            return block.GetBytes(offsetInBlock, length);
        }
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
        var decompressedLength = DecompressedBlockLengths[blockIndex];
        var decompressedBlock = DecompressedBlock
            .FromCompressed(
                blockIndex, compressedBytes,
                CompressionMethod, CompressionLevel, decompressedLength);
        decompressedBlock.LastAccessTicks = Environment.TickCount64; 
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
        CircularBlockCache.Clear();
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
        CircularBlockCache.Clear();
        Dispose();
        FileStreamProvider.DeleteFile(FilePath);
    }

    public void ClearContent()
    {
        // first 4 bytes hold the block size.
        FileStream.SetLength(MetaDataSize);
        FileStream.Seek(MetaDataSize, SeekOrigin.Begin);

        NextBlockIndex = 0;
        CompressedBlockPositions.Clear();
        CompressedBlockLengths.Clear();
        DecompressedBlockLengths.Clear();
        LastBlockLength = 0;
        CircularBlockCache.Clear();
    }

    public void SealDevice()
    {
        var nextBlock = NextBlock;
        if (nextBlock != null)
        {
            AppendBlock(nextBlock);
            LastBlockLength = nextBlock.Length;
            --NextBlockIndex;
            NextBlock = null;
        }
        WriteCompressedBlockPositionsAndLengths();
    }

    void WriteCompressedBlockPositionsAndLengths()
    {
        var offset = CompressedBlockPositions.LastOrDefault(MetaDataSize);
        offset += CompressedBlockLengths.LastOrDefault();
        FileStream.SetLength(offset);
        FileStream.Position = offset;
        var positions = CompressedBlockPositions;
        var lengths1 = CompressedBlockLengths;
        var lengths2 = DecompressedBlockLengths;
        var len = positions.Count;
        var bw = BinaryWriter;
        for (var i = 0; i < len; ++i)
        {
            bw.Write(positions[i]);
            bw.Write(lengths1[i]);
            bw.Write(lengths2[i]);
        }
        bw.Write(positions.Count);
        bw.Write(offset);
        FileStream.Flush(true);
    }

    (List<long> positions,
     List<int> compressedLengths,
     List<int> decompressedLengths) ReadCompressedBlockPositionsAndLengths()
    {
        FileStream.Seek(- sizeof(int) - sizeof(long), SeekOrigin.End);
        var br = BinaryReader;
        var len = br.ReadInt32();
        var offset = br.ReadInt64();
        FileStream.Seek(offset, SeekOrigin.Begin);
        var positions = new List<long>(len);
        var compressedLengths = new List<int>(len);
        var decompressedLengths = new List<int>(len);
        for (var i = 0; i < len; ++i)
        {
            positions.Add(br.ReadInt64());
            compressedLengths.Add(br.ReadInt32());
            decompressedLengths.Add(br.ReadInt32());
        }
        return (positions, compressedLengths, decompressedLengths);
    }

    public int ReleaseReadBuffers(long ticks)
    {
        if (NextBlock != null && 
            NextBlock.LastAccessTicks <= ticks)
        {
            NextBlock = null;
        }
        var removed = 0;
        var blocks = CircularBlockCache.ToArray();
        var len = blocks.Length;
        for (int i = 0; i < len; i++)
        {
            var b = blocks[i];
            if (b.LastAccessTicks > ticks)
                continue;
            CircularBlockCache.RemoveBlock(b.BlockIndex);
            ++removed;
        }
        return removed;
    }
}
