using System.IO.Compression;

namespace Tenray.ZoneTree.Segments.Disk;

public class DecompressedBlock
{
    public int BlockIndex { get; private set; }

    public int Length { get; private set; }

    private byte[] Bytes { get; set; }
    
    public bool IsFull => Length == Bytes.Length;

    public long LastAccessTicks { get; set; }

    public DecompressedBlock(int blockIndex, int blockSize)
    {
        BlockIndex = blockIndex;
        Length = 0;
        Bytes = new byte[blockSize];
    }

    public DecompressedBlock(int blockIndex, byte[] bytes)
    {
        BlockIndex = blockIndex;
        Length = bytes.Length;
        Bytes = bytes;
    }

    public int Append(ReadOnlySpan<byte> data)
    {
        var remainingLength = Bytes.Length - Length;
        var copyLength = Math.Min(data.Length, remainingLength);
        data.Slice(0, copyLength).CopyTo(Bytes.AsSpan(Length));
        Length += copyLength;
        return copyLength;
    }

    public byte[] Compress()
    {
        var span = Bytes.AsSpan(0, Length);
        using var msOutput = new MemoryStream();
        using var gzs = new GZipStream(msOutput, CompressionLevel.Fastest, false);
        gzs.Write(span);
        gzs.Flush();
        return msOutput.ToArray();
    }

    public static DecompressedBlock FromCompressed(int blockIndex, byte[] compressedBytes)
    {
        try
        {
            using var msInput = new MemoryStream(compressedBytes);
            using var msOutput = new MemoryStream();
            using var gzs = new GZipStream(msInput, CompressionMode.Decompress);
            gzs.CopyTo(msOutput);
            var decompressed = msOutput.ToArray();
            return new DecompressedBlock(blockIndex, decompressed);
        }
        catch(InvalidDataException)
        {
            throw;
        }
    }

    public byte[] GetBytes(int offset, int length)
    {
        if (offset + length > Length)
            length = Length - offset;
        if (offset == 0 && length == Bytes.Length && Length == length)
            return Bytes;
        return Bytes.AsSpan().Slice(offset, length).ToArray();
    }

    public int TrimRight(long length)
    {
        var amount = Math.Min(length, Length);
        var newSize = Length - amount;
        Bytes = Bytes.AsSpan(0, (int)newSize).ToArray();
        return (int)amount;
    }
}