using System.Collections.Concurrent;
using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree.Segments.Disk;

public class CircularBlockCache
{
    readonly ILogger Logger;

    volatile int MaxCachedBlockCount;

    ConcurrentDictionary<int, DecompressedBlock> Table
        = new();
    readonly ConcurrentDictionary<int, long> LastReplacementTicks
        = new();

    public int Count => Table.Count;

    /// <summary>
    /// Block replacement warning ticks length in millisecond.
    /// Default value is 1000 ms;
    /// </summary>
    public long BlockReplacementWarningTicksLength = 1000;

    public CircularBlockCache(ILogger logger, int maxCachedBlockCount)
    {
        Logger = logger;
        MaxCachedBlockCount = maxCachedBlockCount;
    }

    public void AddBlock(DecompressedBlock block)
    {
        var blockIndex = block.BlockIndex;
        var circularIndex = blockIndex % MaxCachedBlockCount;

        var increaseBlockCache = false;
        var currentBlockCacheCapacity = MaxCachedBlockCount;
        Table.AddOrUpdate(circularIndex,
            block,
            (key, existingBlock) =>
            {
                var existingBlockIndex = existingBlock.BlockIndex;
                if (existingBlockIndex == blockIndex)
                    return block;
                var now = Environment.TickCount64;
                LastReplacementTicks.TryGetValue(existingBlockIndex, out var lastReplacementTicks);
                var delta = now - lastReplacementTicks;
                if (delta <
                    BlockReplacementWarningTicksLength)
                {
                    var warning = new BlockCacheTooFrequentReplacementWarning
                    {
                        BlockIndex = existingBlockIndex,
                        Delta = delta,
                        CurrentCacheCapacity = currentBlockCacheCapacity,

                    };
                    Logger.LogWarning(warning);
                    increaseBlockCache = true;
                }

                LastReplacementTicks.AddOrUpdate(existingBlockIndex, now, (k, o) => now);
                return block;
            });
        if (increaseBlockCache)
            IncreaseBlockCache(currentBlockCacheCapacity);
    }

    void IncreaseBlockCache(int currentCacheCapacity)
    {
        lock (this)
        {
            if (currentCacheCapacity != MaxCachedBlockCount)
                return;
            var newCapacity = currentCacheCapacity * 2;
            var table = new ConcurrentDictionary<int, DecompressedBlock>();
            var items = Table.ToArray();
            foreach(var item in items)
            {
                var block = item.Value;
                var circularIndex = block.BlockIndex % newCapacity;
                table.TryAdd(circularIndex, block);
            }
            MaxCachedBlockCount = newCapacity;
            Table = table;
            Logger.LogInfo($"Block cache capacity increased from {currentCacheCapacity} to {newCapacity}.");
        }        
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

    public class BlockCacheTooFrequentReplacementWarning
    {
        public int BlockIndex { get; set; }

        public long Delta { get; set; }

        public int CurrentCacheCapacity { get; set; }

        public override string ToString()
        {
            var str = $"Reads are slower. Replacement frequency is too high. (Delta: {Delta})" +
                Environment.NewLine +
                $"\tBlock index: {BlockIndex}" +
                Environment.NewLine +
                "\tAuto performance tuning will increase block cache capacity. " +
                Environment.NewLine +
                $"\tCurrent Capacity:{CurrentCacheCapacity}" +
                Environment.NewLine;
            return str;
        }
    }
}

