using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.WAL;

public sealed class CompressedFileStream : Stream, IDisposable
{
    readonly int BlockSize;

    readonly FileStream FileStream;

    DecompressedBlock NextBlock;

    int NextBlockIndex;

    int BlockPosition;

    BinaryReader BinaryReader;

    BinaryWriter BinaryWriter;

    int _length;

    public string FilePath { get; }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => _length;

    public override long Position { get; set; }

    public CompressedFileStream(string filePath, int blockSize = 32768)
    {
        FilePath = filePath;
        FileStream = new FileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, 4096, false);
        BinaryReader = new BinaryReader(FileStream, null, true);
        BinaryWriter = new BinaryWriter(FileStream, null, true);
        BlockSize = blockSize;
        NextBlockIndex = -1;
        SkipToTheEnd();
        ReadNextBlock();
        BlockPosition = NextBlock.Length;
    }

    void ReadNextBlock()
    {
        ++NextBlockIndex;
        BlockPosition = 0;
        if (FileStream.Position == FileStream.Length)
        {
            AddNewBlock();
            return;
        }
        var compressedBlockSize = BinaryReader.ReadInt32();
        BinaryReader.ReadInt32(); // block size
        var bytes = BinaryReader.ReadBytes(compressedBlockSize);
        NextBlock = DecompressedBlock.FromCompressed(NextBlockIndex, bytes);
    }

    void AddNewBlock()
    {
        NextBlock = new DecompressedBlock(NextBlockIndex, BlockSize);
    }

    void SkipToTheEnd()
    {
        var len = FileStream.Length;
        var position = FileStream.Position;
        while (true)
        {
            ++NextBlockIndex;
            if (position == len)
                break;
            var compressedBlockSize = BinaryReader.ReadInt32();
            position += compressedBlockSize;

            var blockSize = BinaryReader.ReadInt32();
            _length += blockSize;
            if (position > len)
            {
                position -= compressedBlockSize;
                _length -= blockSize;
                FileStream.SetLength(position);
                break;
            }
        }
        --NextBlockIndex;
        FileStream.Position = position;
        Position = _length;
    }

    public override void Flush()
    {
    }
    int ReadInternal(byte[] buffer, int offset, int count)
    {
        var bytes = NextBlock.GetBytes(BlockPosition, count);
        BlockPosition += bytes.Length;
        Array.Copy(bytes, 0, buffer, offset, bytes.Length);
        return bytes.Length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var len = 0;
        while (true)
        {
            var readLen = ReadInternal(buffer, offset, count);
            len += readLen;
            if (len == count)
                return len;
            if (!NextBlock.IsFull)
                return len;
            offset += readLen;
            count -= readLen;
            ReadNextBlock();
        }
    }

    void Skip(long offset)
    {
        var b = BlockPosition + offset;
        while (b > NextBlock.Length)
        {
            if(!NextBlock.IsFull)
                break;
            ReadNextBlock();
            b -= NextBlock.Length;
        }
        Position += (int)offset;
        BlockPosition = (int)b;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (offset < 0)
            throw new NotSupportedException("Negative seek is not supported.");
        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;
                FileStream.Position = 0;
                Position = 0;
                ReadNextBlock();
                Skip(offset);
                break;
            case SeekOrigin.Current:
                Skip(offset);
                Position += offset;
                break;
            case SeekOrigin.End:
                Position = Length;
                FileStream.Position = 0;
                SkipToTheEnd();
                ReadNextBlock();
                break;
        }
        return Position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var copyLen = 0;
        while (true)
        {
            copyLen += NextBlock.Append(buffer.AsSpan(offset, count));
            _length += copyLen;
            Position += copyLen;
            BlockPosition = NextBlock.Length;
            if (NextBlock.IsFull)
                AppendBlock();

            if (copyLen < count)
                ReadNextBlock();
            else
                return;
        }
    }

    private void AppendBlock()
    {
        var compressedBytes = NextBlock.Compress();
        BinaryWriter.Write(compressedBytes.Length);
        BinaryWriter.Write(NextBlock.Length);
        BinaryWriter.Write(compressedBytes);
    }

    void IDisposable.Dispose()
    {
        BinaryReader.Dispose();
        BinaryWriter.Dispose();
        FileStream.Dispose();
    }
}
