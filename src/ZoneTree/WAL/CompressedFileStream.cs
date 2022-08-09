using System.IO.Compression;
using System.Text;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.WAL;

public sealed class CompressedFileStream : Stream, IDisposable
{
    readonly int BlockSize;

    readonly IFileStream FileStream;

    readonly IFileStream TailStream;

    DecompressedBlock TailBlock;

    DecompressedBlock CurrentBlock;

    int CurrentBlockPosition;

    readonly BinaryReader BinaryReader;

    readonly BinaryWriter BinaryWriter;

    readonly BinaryReader BinaryChunkReader;
    
    readonly BinaryWriter BinaryChunkWriter;

    long _length;

    readonly Task TailWriter;

    volatile bool IsTailWriterRunning;

    readonly int TailWriterJobInterval;

    volatile int LastWrittenTailIndex = -1;

    volatile int LastWrittenTailLength = 0;

    volatile bool IsClosed = false;

    public string FilePath { get; }

    public override bool CanRead => FileStream.CanRead;

    public override bool CanSeek => FileStream.CanRead;

    public override bool CanWrite => FileStream.CanWrite;

    public override long Length => _length;

    public override long Position { get; set; }

    public CompressedFileStream(
        IFileStreamProvider fileStreamProvider,
        string filePath,
        int blockSize,
        bool enableTailWriterJob,
        int tailWriterJobInterval)
    {
        FilePath = filePath;
        FileStream = fileStreamProvider.CreateFileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, BlockSize);

        TailStream = fileStreamProvider.CreateFileStream(filePath + ".tail",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, BlockSize);

        BinaryReader = new BinaryReader(FileStream.ToStream(), Encoding.UTF8, true);
        BinaryWriter = new BinaryWriter(FileStream.ToStream(), Encoding.UTF8, true);

        BinaryChunkReader = new BinaryReader(TailStream.ToStream(), Encoding.UTF8, true);
        BinaryChunkWriter = new BinaryWriter(TailStream.ToStream(), Encoding.UTF8, true);

        BlockSize = blockSize;
        LoadTail();
        var lastBlockIndex = SkipToTheEnd();

        // discard tail record if a greater block index exists in the main file.
        if (lastBlockIndex >= TailBlock.BlockIndex)
        {
            // if WAL had incomplete block in the end,
            // it was truncated in the skip to the end method.
            // Hence it is safe to discard the tail block
            // if the block index is equal or smaller.
            _length -= TailBlock.Length;
            Position -= TailBlock.Length;
            TailBlock = new DecompressedBlock(lastBlockIndex + 1, BlockSize);
        }
        CurrentBlockPosition = 0;
        CurrentBlock = TailBlock;
        TailWriterJobInterval = tailWriterJobInterval;
        if (enableTailWriterJob)
        {
            TailWriter = Task.Factory.StartNew(() => TailWriteLoop(),
                TaskCreationOptions.LongRunning);
        }
    }

    void TailWriteLoop()
    {
        IsTailWriterRunning = true;
        while(IsTailWriterRunning)
        {
            WriteTail();
            if (TailWriterJobInterval == 0)
                Thread.Yield();
            else
                Thread.Sleep(TailWriterJobInterval);
        }
    }

    void StopTailWriter()
    {
        IsTailWriterRunning = false;
        TailWriter?.Wait();
    }

    void LoadTail()
    {
        // read tail with tail tolerance.
        TailStream.Position = 0;
        var br = BinaryChunkReader;

        var bytes = br.ReadBytes(2 * sizeof(int));
        var bytesLen = bytes.Length;
        if (bytesLen < sizeof(int))
        {
            TailBlock = new DecompressedBlock(0, BlockSize);
            return;
        }
        var blockIndex =
            BinarySerializerHelper.FromByteArray<int>(bytes, 0);
        if (bytesLen < 2 * sizeof(int))
        {
            TailBlock = new DecompressedBlock(blockIndex, BlockSize);
            return;
        }
        var decompressedLength =
            BinarySerializerHelper.FromByteArray<int>(bytes, sizeof(int));
        if (decompressedLength < 0)
        {
            TailBlock = new DecompressedBlock(blockIndex, BlockSize);
            return;
        }
        var len = TailStream.Length;
        if (len < 2 * sizeof(int) + decompressedLength)
        {
            decompressedLength = (int)(len - 2 * sizeof(int));
            if (decompressedLength <= 0)
            {
                TailBlock = new DecompressedBlock(blockIndex, BlockSize);
                return;
            }
        }
        bytes = br.ReadBytes(decompressedLength);
        TailBlock = new DecompressedBlock(blockIndex, bytes);
    }

    public void WriteTail()
    {
        var tailBlock = TailBlock;
        if (tailBlock.BlockIndex < LastWrittenTailIndex)
            return;
        if (tailBlock.BlockIndex == LastWrittenTailIndex &&
            tailBlock.Length == LastWrittenTailLength)
            return;
        if (IsClosed || !TailStream.CanWrite)
            return;
        lock (TailStream)
        {
            tailBlock = TailBlock;
            if (tailBlock.BlockIndex < LastWrittenTailIndex)
                return;
            if (tailBlock.BlockIndex == LastWrittenTailIndex &&
                tailBlock.Length == LastWrittenTailLength)
                return;
            if (IsClosed || !TailStream.CanWrite)
                return;

            TailStream.Position = 0;
            BinaryChunkWriter.Write(tailBlock.BlockIndex);
            BinaryChunkWriter.Write(tailBlock.Length);
            var bytes = tailBlock.GetBytes(0, tailBlock.Length);
            BinaryChunkWriter.Write(bytes);
            TailStream.Flush(true);
            LastWrittenTailIndex = tailBlock.BlockIndex;
            LastWrittenTailLength = tailBlock.Length;
        }
    }

    bool LoadNextBlock()
    {
        if (FileStream.Position == FileStream.Length)
        {
            if (CurrentBlock.BlockIndex == TailBlock.BlockIndex)
                return false;
            CurrentBlockPosition = 0;
            CurrentBlock = TailBlock;
            return true;
        }
        CurrentBlockPosition = 0;
        var bytes = BinaryReader.ReadBytes(3 * sizeof(int));
        var blockIndex =
            BinarySerializerHelper.FromByteArray<int>(bytes, 0);
        var compressedBlockSize = 
            BinarySerializerHelper.FromByteArray<int>(bytes, sizeof(int));
#if DEBUG
        var blockSize = 
            BinarySerializerHelper.FromByteArray<int>(bytes, 2 * sizeof(int));
#endif

        bytes = BinaryReader.ReadBytes(compressedBlockSize);

        var nextBlock = DecompressedBlock.FromCompressed(blockIndex, bytes);
        CurrentBlock = nextBlock;
        return true;
    }

    int SkipToTheEnd()
    {
        Position = 0;
        _length = 0;
        FileStream.Position = 0;
        var len = FileStream.Length;
        var physicalPosition = 0;
        var lastBlockIndex = 0;
        while (true)
        {
            if (physicalPosition == len)
                break;

            var blockStartPosition = physicalPosition;

            var bytes = BinaryReader.ReadBytes(3 * sizeof(int));
            if (bytes.Length != 3 * sizeof(int))
            {
                // truncates partially written compressed block.
                // because the last block content is also written in tail block.
                FileStream.Position = blockStartPosition;
                FileStream.SetLength(blockStartPosition);
                break;
            }
            var blockIndex =
                BinarySerializerHelper.FromByteArray<int>(bytes, 0);
            var compressedBlockSize =
                BinarySerializerHelper.FromByteArray<int>(bytes, sizeof(int));
            var blockSize =
                BinarySerializerHelper.FromByteArray<int>(bytes, 2 * sizeof(int));

            if (physicalPosition > len)
            {
                // truncates partially written compressed block.
                // because the last block content is also written in tail block.
                FileStream.Position = blockStartPosition;
                FileStream.SetLength(blockStartPosition);
                break;
            }
            physicalPosition += sizeof(int) * 3;
            physicalPosition += compressedBlockSize;
            FileStream.Position = physicalPosition;
            _length += blockSize;
            lastBlockIndex = blockIndex;
        }
        _length += TailBlock.Length;
        CurrentBlock = TailBlock;
        CurrentBlockPosition = TailBlock.Length;
        Position = _length;
        return lastBlockIndex;
    }

    public override void Flush()
    {
    }

    int ReadInternal(byte[] buffer, int offset, int count)
    {
        var bytes = CurrentBlock.GetBytes(CurrentBlockPosition, count);
        CurrentBlockPosition += bytes.Length;
        Position += bytes.Length;
        Array.Copy(bytes, 0, buffer, offset, bytes.Length);
        return bytes.Length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var initialCount = count;
        var len = 0;
        while (true)
        {
            var readLen = ReadInternal(buffer, offset, count);
            len += readLen;
            if (len == initialCount)
                return len;
            if (!CurrentBlock.IsFull)
                return len;
            offset += readLen;
            count -= readLen;
            if (!LoadNextBlock())
                return len;
        }
    }

    void Skip(long offset)
    {
        if (offset <= 0)
            return;
        var b = CurrentBlockPosition + offset;
        while (b > CurrentBlock.Length)
        {
            if(!CurrentBlock.IsFull)
                break;
            if (!LoadNextBlock())
                break;
            b -= CurrentBlock.Length;
        }
        Position += offset;
        CurrentBlockPosition = (int)b;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset < 0)
            throw new NotSupportedException("Negative seek is not supported.");
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = 0;
                FileStream.Position = 0;
                CurrentBlockPosition = 0;
                Position = 0;
                LoadNextBlock();
                Skip(offset);
                break;
            case SeekOrigin.Current:
                Skip(offset);
                break;
            case SeekOrigin.End:
                SkipToTheEnd();
                break;
        }
        return Position;
    }

    public override void SetLength(long length)
    {
        if (length < 0)
            throw new Exception("File truncatedLength cannot be negative number!");

        if (length > _length)
            throw new Exception("Compressed file cannot be expanded with empty bytes.");

        if (length != 0)
        {
            // Sync with tail writer
            lock (TailStream)
            {
                TruncateFile(length);
            }
            return;
        }
        FileStream.SetLength(0);
        TailBlock = new DecompressedBlock(TailBlock.BlockIndex, BlockSize);
        SkipToTheEnd();
        CurrentBlockPosition = 0;
        LastWrittenTailIndex = -1;
        WriteTail();
    }

    private void TruncateFile(long truncatedLength)
    {
        FileStream.Position = 0;
        var len = FileStream.Length;
        var remainingTruncation = _length - truncatedLength;
        var trimmed = TailBlock.TrimRight(remainingTruncation);
        truncatedLength -= trimmed;
        _length -= trimmed;
        if (truncatedLength == 0)
            return;
        LastWrittenTailLength = 0;
        WriteTail();

        remainingTruncation = _length - truncatedLength;
        var physicalPosition = 0;
        var off = 0;
        while (true)
        {
            if (physicalPosition >= len)
                break;

            var bytes = BinaryReader.ReadBytes(3 * sizeof(int));
            if (bytes.Length != 3 * sizeof(int))
            {
                FileStream.Position = physicalPosition;
                FileStream.SetLength(physicalPosition);
                break;
            }
#if DEBUG
            var blockIndex =
                BinarySerializerHelper.FromByteArray<int>(bytes, 0);
#endif
            var compressedBlockSize =
                BinarySerializerHelper.FromByteArray<int>(bytes, sizeof(int));
            var blockSize =
                BinarySerializerHelper.FromByteArray<int>(bytes, 2 * sizeof(int));
            if (off + blockSize > truncatedLength)
            {
                var diff = truncatedLength - off;
                bytes = BinaryReader.ReadBytes(compressedBlockSize);
                var truncatedBytes = DecompressedBlock
                    .FromCompressed(0, bytes).GetBytes(0, (int)diff);
                var compressedBytes = new DecompressedBlock(0, truncatedBytes).Compress();
                FileStream.Position = physicalPosition + sizeof(int);
                var bw = BinaryWriter;
                bw.Write(compressedBytes.Length);
                bw.Write(truncatedBytes.Length);
                bw.Write(compressedBytes);
                FileStream.SetLength(FileStream.Position);
                _length -= (int)remainingTruncation;
                break;
            }
            physicalPosition += sizeof(int) * 3;
            physicalPosition += compressedBlockSize;
            FileStream.Position = physicalPosition;
            off += blockSize;
        }
        FileStream.Position = FileStream.Length;
        CurrentBlock = TailBlock;
        CurrentBlockPosition = TailBlock.Length;
        Position = _length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (this)
        {
            if (Position != Length)
                throw new Exception("Compressed File Stream can only write to the end of the file.");

            var totalWriteLen = 0;
            var totalCount = count;
            while (true)
            {
                var writeLen = TailBlock.Append(buffer.AsSpan(offset, count));                
                totalWriteLen += writeLen;
                _length += writeLen;
                Position += writeLen;
                offset += writeLen;
                count -= writeLen;
                CurrentBlockPosition = CurrentBlock.Length;
                if (TailBlock.IsFull)
                {
                    CommitTailBlock();
                    CurrentBlock = TailBlock;
                    CurrentBlockPosition = 0;
                }
                if (totalWriteLen < totalCount)
                    continue;
                else
                    return;
            }
        }
    }

    private void CommitTailBlock()
    {
        if (IsClosed || !FileStream.CanWrite)
            return;
        var tailBlock = TailBlock;
        var compressedBytes = tailBlock.Compress();
        var bw = BinaryWriter;
        bw.Write(tailBlock.BlockIndex);
        bw.Write(compressedBytes.Length);
        bw.Write(tailBlock.Length);
        bw.Write(compressedBytes);
        bw.Flush();
        TailBlock = new DecompressedBlock(tailBlock.BlockIndex + 1, BlockSize);
    }

    public new void Dispose()
    {
        Close();
    }

    public override void Close()
    {
        if (IsClosed)
            return;
        lock (this)
        {
            if (IsClosed)
                return;
            StopTailWriter();
            WriteTail();
            /*
             * Do not flush any file stream
             * and do not dispose any binary writer.
             * Because disposing binary writer also fires a flush.
             * BinaryWriter and binaryReaders are created
             * with leaveOpen flag = true.
             */
            FileStream.Close();
            TailStream.Close();
            IsClosed = true;
        }
    }

    public byte[] GetFileContent()
    {
        var currentPosition = FileStream.Position;
        FileStream.Position = 0;
        var bytes = new byte[FileStream.Length];
        FileStream.Read(bytes);
        FileStream.Position = currentPosition;
        return bytes;
    }

    public byte[] GetFileContentIncludingTail()
    {
        lock (this)
        {
            var tailBlock = TailBlock;
            var compressedBytes = tailBlock.Compress();
            var currentPosition = FileStream.Position;
            FileStream.Position = 0;
            var bytes = new byte[FileStream.Length + compressedBytes.Length + 3 * sizeof(int)];
            using var ms = new MemoryStream(bytes);
            using var bw = new BinaryWriter(ms);
            FileStream.CopyTo(ms);
            bw.Write(tailBlock.BlockIndex);
            bw.Write(compressedBytes.Length);
            bw.Write(tailBlock.Length);
            bw.Write(compressedBytes);
            ms.Flush();
            FileStream.Position = currentPosition;
            return bytes;
        }
    }

    protected override void Dispose(bool disposing)
    {
        Dispose();
    }
}