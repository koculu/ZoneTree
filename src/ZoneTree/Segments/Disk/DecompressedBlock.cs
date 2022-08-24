using System.IO.Compression;
using Tenray.ZoneTree.Compression;

namespace Tenray.ZoneTree.Segments.Disk;

public class DecompressedBlock
{
    public CompressionMethod CompressionMethod { get; }

    public int BlockIndex { get; private set; }

    public volatile int _length;

    public int Length
    {
        get => _length;
        set => _length = value;
    }

    volatile byte[] Bytes;
    
    public bool IsFull => Length == Bytes.Length;

    long _lastAccessTicks;

    public long LastAccessTicks {
        get => Volatile.Read(ref _lastAccessTicks); 
        set => Volatile.Write(ref _lastAccessTicks, value);
    }

    public DecompressedBlock(int blockIndex, int blockSize, CompressionMethod compressionMethod)
    {
        BlockIndex = blockIndex;
        Length = 0;
        Bytes = new byte[blockSize];
        CompressionMethod = compressionMethod;
    }

    public DecompressedBlock(int blockIndex, byte[] bytes, CompressionMethod method)
    {
        BlockIndex = blockIndex;
        Length = bytes.Length;
        Bytes = bytes;
        CompressionMethod = method;
    }

    public int Append(ReadOnlySpan<byte> data)
    {
        var remainingLength = Bytes.Length - Length;
        var copyLength = Math.Min(data.Length, remainingLength);
        data[..copyLength].CopyTo(Bytes.AsSpan(Length));
        Length += copyLength;
        return copyLength;
    }

    public byte[] Compress()
    {
        var span = Bytes.AsSpan(0, Length);
        return DataCompression.Compress(CompressionMethod, span);
    }

    public static DecompressedBlock FromCompressed(int blockIndex, byte[] compressedBytes, CompressionMethod method)
    {
        var decompressed = DataCompression.Decompress(method, compressedBytes);
        return new DecompressedBlock(blockIndex, decompressed, method);
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
        Length = (int)newSize;
        return (int)amount;
    }
}