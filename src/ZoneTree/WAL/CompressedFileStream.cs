using System.Text;
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

    public CompressedFileStream(string filePath, int blockSize)
    {
        FilePath = filePath;
        FileStream = new FileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read, 4096, false);
        BinaryReader = new BinaryReader(FileStream, Encoding.UTF8, true);
        BinaryWriter = new BinaryWriter(FileStream, Encoding.UTF8, true);
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
        var blockSize = BinaryReader.ReadInt32();
        var bytes = BinaryReader.ReadBytes(compressedBlockSize);
        NextBlock = DecompressedBlock.FromCompressed(NextBlockIndex, bytes);
    }

    void AddNewBlock()
    {
        NextBlock = new DecompressedBlock(NextBlockIndex, BlockSize);
    }

    void SkipToTheEnd()
    {
        Position = 0;
        _length = 0;
        NextBlockIndex = -1;
        NextBlock = null;
        FileStream.Position = 0;
        var len = FileStream.Length;
        var position = 0;
        while (true)
        {
            ++NextBlockIndex;
            if (position == len)
                break;
            var compressedBlockSize = BinaryReader.ReadInt32();
            var blockSize = BinaryReader.ReadInt32();
            position += sizeof(int) * 2;
            position += compressedBlockSize;
            FileStream.Position = position;

            _length += blockSize;
            if (position > len)
            {
                position -= compressedBlockSize;
                position -= sizeof(int) * 2;
                FileStream.Position = position;
                _length -= blockSize;
                FileStream.SetLength(position);
                break;
            }
        }
        --NextBlockIndex;
        Position = _length;
    }

    public override void Flush()
    {
    }

    int ReadInternal(byte[] buffer, int offset, int count)
    {
        var bytes = NextBlock.GetBytes(BlockPosition, count);
        BlockPosition += bytes.Length;
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
            if (!NextBlock.IsFull)
                return len;
            offset += readLen;
            count -= readLen;
            ReadNextBlock();
        }
    }

    void Skip(long offset)
    {
        if (offset <= 0)
            return;
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
                Position = 0;
                FileStream.Position = 0;
                Position = 0;
                NextBlockIndex = -1;
                ReadNextBlock();
                Skip(offset);
                break;
            case SeekOrigin.Current:
                Skip(offset);
                break;
            case SeekOrigin.End:
                SkipToTheEnd();
                ReadNextBlock();
                break;
        }
        return Position;
    }

    public override void SetLength(long value)
    {
        if (value != 0)
            throw new NotSupportedException();
        FileStream.SetLength(0);
        SkipToTheEnd();
        ReadNextBlock();
        BlockPosition = NextBlock.Length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var totalWriteLen = 0;
        var totalCount = count;
        while (true)
        {
            var writeLen = NextBlock.Append(buffer.AsSpan(offset, count));
            totalWriteLen += writeLen;
            _length += writeLen;
            Position += writeLen;
            offset += writeLen;
            count -= writeLen;
            BlockPosition = NextBlock.Length;
            if (NextBlock.IsFull)
            {
                CompressBlockAndWrite();
                ReadNextBlock();
            }
            if (totalWriteLen < totalCount)
                continue;
            else
                return;
        }
    }

    private void CompressBlockAndWrite()
    {
        if (!FileStream.CanWrite || NextBlock == null)
            return;
        var compressedBytes = NextBlock.Compress();
        BinaryWriter.Write(compressedBytes.Length);
        BinaryWriter.Write(NextBlock.Length);
        BinaryWriter.Write(compressedBytes);
        BinaryWriter.Flush();
        NextBlock = null;
    }

    public void SealStream()
    {
        if (NextBlock != null && NextBlock.Length > 0)
            CompressBlockAndWrite();
    }

    void IDisposable.Dispose()
    {
        SealStream();
        BinaryReader.Dispose();
        BinaryWriter.Dispose();
        FileStream.Dispose();
        base.Dispose();
    }

    public override void Close()
    {
        SealStream();
        FileStream.Close();
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
}
