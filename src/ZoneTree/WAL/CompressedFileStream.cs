﻿using System.Text;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.WAL;

#pragma warning disable CA1063
#pragma warning disable CA2213
#pragma warning disable CA2215

public sealed class CompressedFileStream : Stream, IDisposable
{
    readonly ILogger Logger;

    readonly int BlockSize;

    readonly CompressionMethod CompressionMethod;

    readonly int CompressionLevel;

    readonly IFileStream FileStream;

    readonly IFileStream TailStream;

    volatile DecompressedBlock TailBlock;

    volatile DecompressedBlock CurrentBlock;

    volatile int CurrentBlockPosition;

    readonly BinaryReader BinaryReader;

    readonly BinaryWriter BinaryWriter;

    readonly BinaryReader BinaryTailReader;

    readonly BinaryWriter BinaryTailWriter;

    long _length;

    volatile bool IsTailWriterRunning;

    readonly int TailWriterJobInterval;

    volatile int LastWrittenTailIndex = -1;

    volatile int LastWrittenTailLength;

    volatile bool IsClosed;

    public string FilePath { get; }

    public override bool CanRead => FileStream.CanRead;

    public override bool CanSeek => FileStream.CanRead;

    public override bool CanWrite => FileStream.CanWrite;

    public override long Length => _length;

    public override long Position { get; set; }

    public struct CompressedFileMeta
    {
        public CompressionMethod CompressionMethod;

        public CompressedFileMeta(CompressionMethod compressionMethod) : this()
        {
            CompressionMethod = compressionMethod;
        }
    }

    const int MetaDataSize = sizeof(byte);

    public CompressedFileStream(
        ILogger logger,
        IFileStreamProvider fileStreamProvider,
        string filePath,
        int blockSize,
        bool enableTailWriterJob,
        int tailWriterJobInterval,
        CompressionMethod compressionMethod,
        int compressionLevel)
    {
        CompressionLevel = compressionLevel;
        Logger = logger;
        FilePath = filePath;
        FileStream = fileStreamProvider.CreateFileStream(filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None, BlockSize);

        TailStream = fileStreamProvider.CreateFileStream(filePath + ".tail",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None, BlockSize);

        BinaryReader = new BinaryReader(FileStream.ToStream(), Encoding.UTF8, true);
        BinaryWriter = new BinaryWriter(FileStream.ToStream(), Encoding.UTF8, true);

        BinaryTailReader = new BinaryReader(TailStream.ToStream(), Encoding.UTF8, true);
        BinaryTailWriter = new BinaryWriter(TailStream.ToStream(), Encoding.UTF8, true);

        BlockSize = blockSize;
        if (FileStream.Length > 0)
        {
            CompressionMethod = ReadMetaData().CompressionMethod;
        }
        else
        {
            CompressionMethod = compressionMethod;
            WriteMetaData();
        }

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
            TailBlock = new DecompressedBlock(
                lastBlockIndex + 1, BlockSize, CompressionMethod, CompressionLevel);
        }
        CurrentBlockPosition = 0;
        CurrentBlock = TailBlock;
        TailWriterJobInterval = tailWriterJobInterval;
        if (enableTailWriterJob)
        {
            Task.Factory.StartNew(
                () => TailWriteLoop(),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    CompressedFileMeta ReadMetaData()
    {
        FileStream.Position = 0;
        return new CompressedFileMeta(
            (CompressionMethod)BinaryReader.ReadByte());
    }

    void WriteMetaData()
    {
        FileStream.Position = 0;
        BinaryWriter.Write((byte)CompressionMethod);
        BinaryWriter.Flush();
        return;
    }

    void TailWriteLoop()
    {
        IsTailWriterRunning = true;
        while (IsTailWriterRunning)
        {
            try
            {
                WriteTail();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }
            if (TailWriterJobInterval == 0)
                Thread.Yield();
            else
                Thread.Sleep(TailWriterJobInterval);
        }
    }

    void StopTailWriter()
    {
        IsTailWriterRunning = false;
    }

    void LoadTail()
    {
        // read tail with tail tolerance.
        TailStream.Position = 0;
        var br = BinaryTailReader;

        var bytes = br.ReadBytes(2 * sizeof(int));
        var bytesLen = bytes.Length;
        if (bytesLen < sizeof(int))
        {
            TailBlock = new DecompressedBlock(0, BlockSize, CompressionMethod, CompressionLevel);
            return;
        }
        var blockIndex =
            BinarySerializerHelper.FromByteArray<int>(bytes, 0);
        if (bytesLen < 2 * sizeof(int))
        {
            TailBlock = new DecompressedBlock(blockIndex, BlockSize, CompressionMethod, CompressionLevel);
            return;
        }
        var decompressedLength =
            BinarySerializerHelper.FromByteArray<int>(bytes, sizeof(int));
        if (decompressedLength < 0)
        {
            TailBlock = new DecompressedBlock(blockIndex, BlockSize, CompressionMethod, CompressionLevel);
            return;
        }
        var len = TailStream.Length;
        if (len < 2 * sizeof(int) + decompressedLength)
        {
            decompressedLength = (int)(len - 2 * sizeof(int));
            if (decompressedLength <= 0)
            {
                TailBlock = new DecompressedBlock(blockIndex, BlockSize, CompressionMethod, CompressionLevel);
                return;
            }
        }
        bytes = br.ReadBytes(decompressedLength);
        TailBlock = new DecompressedBlock(blockIndex, bytes, CompressionMethod, CompressionLevel);
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
        lock (this)
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
            BinaryTailWriter.Write(tailBlock.BlockIndex);
            BinaryTailWriter.Write(tailBlock.Length);
            var bytes = tailBlock.GetBytes(0, tailBlock.Length);
            BinaryTailWriter.Write(bytes);
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
        var blockSize =
            BinarySerializerHelper.FromByteArray<int>(bytes, 2 * sizeof(int));

        bytes = BinaryReader.ReadBytes(compressedBlockSize);

        var nextBlock = DecompressedBlock
            .FromCompressed(
                blockIndex, bytes,
                CompressionMethod, CompressionLevel,
                blockSize);
        CurrentBlock = nextBlock;
        return true;
    }

    int SkipToTheEnd()
    {
        Position = 0;
        _length = 0;
        FileStream.Position = MetaDataSize;
        var len = FileStream.Length;
        var physicalPosition = MetaDataSize;
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
            if (!CurrentBlock.IsFull)
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
                FileStream.Position = MetaDataSize;
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

    public override void SetLength(long value)
    {
        if (value < 0)
            throw new Exception("File truncatedLength cannot be negative number!");

        if (value > _length)
            throw new Exception("Compressed file cannot be expanded with empty bytes.");

        if (value != 0)
        {
            // Sync with tail writer
            lock (this)
            {
                TruncateFile(value);
            }
            return;
        }
        FileStream.SetLength(MetaDataSize);
        FileStream.Position = MetaDataSize;
        TailBlock = new DecompressedBlock(TailBlock.BlockIndex, BlockSize, CompressionMethod, CompressionLevel);
        SkipToTheEnd();
        CurrentBlockPosition = 0;
        LastWrittenTailIndex = -1;
        WriteTail();
    }

    void TruncateFile(long truncatedLength)
    {
        FileStream.Position = MetaDataSize;
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
        var physicalPosition = MetaDataSize;
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
                    .FromCompressed(
                        0, bytes,
                        CompressionMethod,
                        CompressionLevel,
                        blockSize).GetBytes(0, (int)diff);
                var compressedBytes = new DecompressedBlock(0, truncatedBytes, CompressionMethod, CompressionLevel).Compress();
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

    void CommitTailBlock()
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
        FileStream.Flush(true);
        TailBlock = new DecompressedBlock(tailBlock.BlockIndex + 1, BlockSize, CompressionMethod, CompressionLevel);
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
        FileStream.Position = MetaDataSize;
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
            FileStream.Position = MetaDataSize;
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

#pragma warning restore CA1063
#pragma warning restore CA2213
#pragma warning restore CA2215