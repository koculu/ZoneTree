using System.IO.Compression;
using System.Text;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.WAL;

public sealed class CompressedFileStream : Stream, IDisposable
{
    readonly int BlockSize;

    readonly FileStream FileStream;

    readonly FileStream TailStream;

    DecompressedBlock TailBlock;

    DecompressedBlock CurrentBlock;

    int CurrentBlockPosition;

    readonly BinaryReader BinaryReader;

    readonly BinaryWriter BinaryWriter;

    readonly BinaryReader BinaryChunkReader;
    
    readonly BinaryWriter BinaryChunkWriter;

    int _length;

    readonly Task TailWriter;

    volatile bool IsTailWriterRunning;

    readonly int TailWriterJobInterval;

    volatile int LastWrittenTailIndex = -1;

    volatile int LastWrittenTailLength = 0;

    public string FilePath { get; }

    public override bool CanRead => FileStream.CanRead;

    public override bool CanSeek => FileStream.CanRead;

    public override bool CanWrite => FileStream.CanWrite;

    public override long Length => _length;

    public override long Position { get; set; }

    public CompressedFileStream(
        string filePath,
        int blockSize,
        bool enableTailWriterJob,
        int tailWriterJobInterval)
    {
        FilePath = filePath;
        FileStream = new FileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, BlockSize, false);

        TailStream = new FileStream(filePath + ".tail",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, BlockSize, false);

        BinaryReader = new BinaryReader(FileStream, Encoding.UTF8, true);
        BinaryWriter = new BinaryWriter(FileStream, Encoding.UTF8, true);

        BinaryChunkReader = new BinaryReader(TailStream, Encoding.UTF8, true);
        BinaryChunkWriter = new BinaryWriter(TailStream, Encoding.UTF8, true);

        BlockSize = blockSize;
        LoadTail();
        var lastBlockIndex = SkipToTheEnd();

        // discard tail record if a greater block index exists in the main file.
        if (lastBlockIndex >= TailBlock.BlockIndex)
        {
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
        var len = TailStream.Length;
        if(len < sizeof(int))
        {
            TailBlock = new DecompressedBlock(0, BlockSize);
            return;
        }
        var blockIndex = BinaryChunkReader.ReadInt32();
        if (len < 2 * sizeof(int))
        {
            TailBlock = new DecompressedBlock(blockIndex, BlockSize);
            return;
        }
        var decompressedLength = BinaryChunkReader.ReadInt32();

        if (len < 2 * sizeof(int) + decompressedLength)
        {
            decompressedLength = (int)(len - 2 * sizeof(int));
            if (decompressedLength <= 0)
            {
                TailBlock = new DecompressedBlock(blockIndex, BlockSize);
                return;
            }
        }
        var decompressed = BinaryChunkReader.ReadBytes(decompressedLength);
        TailBlock = new DecompressedBlock(blockIndex, decompressed);
    }

    public void WriteTail()
    {
        var tailBlock = TailBlock;
        if (tailBlock.BlockIndex == LastWrittenTailIndex &&
            tailBlock.Length == LastWrittenTailLength)
            return;
        if (!TailStream.CanWrite)
            return;
        lock (tailBlock)
        {
            TailStream.Position = 0;
            BinaryChunkWriter.Write(tailBlock.BlockIndex);
            BinaryChunkWriter.Write(tailBlock.Length);
            var bytes = tailBlock.GetBytes(0, tailBlock.Length);
            BinaryChunkWriter.Write(bytes);
            TailStream.Flush();
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
        var blockIndex = BinaryReader.ReadInt32();
        var compressedBlockSize = BinaryReader.ReadInt32();
        var blockSize = BinaryReader.ReadInt32();
        var bytes = BinaryReader.ReadBytes(compressedBlockSize);

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
            
            var blockIndex = BinaryReader.ReadInt32();
            var compressedBlockSize = BinaryReader.ReadInt32();
            var blockSize = BinaryReader.ReadInt32();
            var blockStartPosition = physicalPosition;
            physicalPosition += sizeof(int) * 3;
            physicalPosition += compressedBlockSize;
            FileStream.Position = physicalPosition;

            _length += blockSize;
            if (physicalPosition > len)
            {
                // truncates partially written compressed block.
                // complete content should be in tail block.
                FileStream.Position = blockStartPosition;
                FileStream.SetLength(blockStartPosition);
                _length -= blockSize;
                break;
            }
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
        Position += (int)offset;
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
            TruncateFile(length);
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
        var remainingTruncation = (int)(_length - truncatedLength);
        var trimmed = TailBlock.TrimRight(remainingTruncation);
        truncatedLength -= trimmed;
        _length -= trimmed;
        if (truncatedLength == 0)
            return;
        LastWrittenTailLength = 0;
        WriteTail();

        remainingTruncation = (int)(_length - truncatedLength);
        var physicalPosition = 0;
        var off = 0;
        while (true)
        {
            if (physicalPosition >= len)
                break;
            var blockIndex = BinaryReader.ReadInt32();
            var compressedBlockSize = BinaryReader.ReadInt32();
            var blockSize = BinaryReader.ReadInt32();
            if (off + blockSize > truncatedLength)
            {
                var diff = (int)(truncatedLength - off);
                var bytes = BinaryReader.ReadBytes(compressedBlockSize);
                var truncatedBytes = DecompressedBlock.FromCompressed(0, bytes).GetBytes(0, diff);
                var compressedBytes = new DecompressedBlock(0, truncatedBytes).Compress();
                FileStream.Position = physicalPosition + sizeof(int);
                BinaryWriter.Write(compressedBytes.Length);
                BinaryWriter.Write(truncatedBytes.Length);
                BinaryWriter.Write(compressedBytes);
                FileStream.SetLength(FileStream.Position);
                _length -= remainingTruncation;
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
        if (Position != Length)
            throw new Exception("Compressed File Stream can only write to the end of the file.");
        lock (this)
        {
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
        if (!FileStream.CanWrite)
            return;
        var tailBlock = TailBlock;
        var compressedBytes = tailBlock.Compress();
        BinaryWriter.Write(tailBlock.BlockIndex);
        BinaryWriter.Write(compressedBytes.Length);
        BinaryWriter.Write(tailBlock.Length);
        BinaryWriter.Write(compressedBytes);
        BinaryWriter.Flush();
        TailBlock = new DecompressedBlock(tailBlock.BlockIndex + 1, BlockSize);
    }

    void IDisposable.Dispose()
    {
        StopTailWriter();
        WriteTail();
        BinaryReader.Dispose();
        BinaryWriter.Dispose();
        FileStream.Dispose();
        BinaryChunkReader.Dispose();
        BinaryChunkWriter.Dispose();
        TailStream.Dispose();
        base.Dispose();
    }

    public override void Close()
    {
        StopTailWriter();
        WriteTail();
        FileStream.Close();
        TailStream.Close();
        base.Close();
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
            var bytes = new byte[(int)FileStream.Length + tailBlock.Length + 3 * sizeof(int)];
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryWriter(ms);
            FileStream.CopyTo(ms);
            br.Write(tailBlock.BlockIndex);
            br.Write(compressedBytes.Length);
            br.Write(tailBlock.Length);
            br.Write(compressedBytes);
            ms.Flush();
            FileStream.Position = currentPosition;
            return bytes;
        }
    }
}