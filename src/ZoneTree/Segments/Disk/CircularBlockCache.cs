using System.Collections.Concurrent;

namespace Tenray.ZoneTree.Segments.Disk;

public class CircularBlockCache
{
    readonly int MaxCachedBlockCount;

    readonly ConcurrentDictionary<int, DecompressedBlock> Table
        = new();

    public int Count => Table.Count;

    public CircularBlockCache(int maxCachedBlockCount)
    {
        MaxCachedBlockCount = maxCachedBlockCount;
    }

    public void AddBlock(DecompressedBlock block)
    {
        var blockIndex = block.BlockIndex;
        var circularIndex = blockIndex % MaxCachedBlockCount;
        Table.AddOrUpdate(circularIndex,
            block,
            (key, existingBlock) => block);
    }

    public void RemoveBlock(int blockIndex)
    {
        var circularIndex = blockIndex % MaxCachedBlockCount;
        Table.TryRemove(circularIndex, out _);
    }

    public bool TryGetBlock(int blockIndex, out DecompressedBlock block)
    {
        var circularIndex = blockIndex % MaxCachedBlockCount;
        if (!Table.TryGetValue(circularIndex, out block))
            return false;
        if (block.BlockIndex != blockIndex)
        {
            block = null;
            return false;
        }
        return true;
    }

    public void Clear()
    {
        Table.Clear();
    }

    public DecompressedBlock[] ToArray()
    {
        return Table.Values.ToArray();
    }
}