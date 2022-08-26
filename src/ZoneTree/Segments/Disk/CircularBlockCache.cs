using System.Collections.Concurrent;
using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree.Segments.Disk;

public class CircularBlockCache
{
    readonly ILogger Logger;

    volatile int MaxCachedBlockCount;

    DecompressedBlock[] Table;

    readonly ConcurrentDictionary<int, long> LastReplacementTicks
        = new();

    public int Count => Table.Length;

    /// <summary>
    /// Block replacement warning duration in millisecond.
    /// Default value is 1000 ms;
    /// </summary>
    readonly long BlockCacheReplacementWarningDuration;

    public CircularBlockCache(
        ILogger logger,
        int maxCachedBlockCount,
        long blockCacheReplacementWarningDuration)
    {
        Logger = logger;
        MaxCachedBlockCount = maxCachedBlockCount;
        BlockCacheReplacementWarningDuration = blockCacheReplacementWarningDuration;
        Table = new DecompressedBlock[maxCachedBlockCount];
    }

    public void AddBlock(DecompressedBlock block)
    {
        var blockIndex = block.BlockIndex;
        var table = Table;
        var currentBlockCacheCapacity = table.Length;
        var circularIndex = blockIndex % currentBlockCacheCapacity;
        var existingBlock = table[circularIndex];
        if (existingBlock == null)
        {
            table[circularIndex] = block;
            return;
        }

        var increaseBlockCache = false;
        var existingBlockIndex = existingBlock.BlockIndex;
        if (existingBlockIndex == blockIndex)
            return;
        var now = Environment.TickCount64;
        LastReplacementTicks.TryGetValue(existingBlockIndex, out var lastReplacementTicks);
        var delta = now - lastReplacementTicks;
        if (delta <
            BlockCacheReplacementWarningDuration)
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
        table[circularIndex] = block;

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
            var newTable = new DecompressedBlock[newCapacity];
            var oldTable = Table;
            var len = oldTable.Length;
            for (int i = 0; i < len; i++)
            {
                var block = oldTable[i];
                if (block == null)
                    continue;
                var circularIndex = block.BlockIndex % newCapacity;
                newTable[circularIndex] = block;
            }
            MaxCachedBlockCount = newCapacity;
            Table = newTable;
            Logger.LogInfo($"Block cache capacity increased from {currentCacheCapacity} to {newCapacity}.");
        }
    }

    public void RemoveBlock(int blockIndex)
    {
        var table = Table;
        var circularIndex = blockIndex % table.Length;
        table[circularIndex] = null;
    }

    public bool TryGetBlock(int blockIndex, out DecompressedBlock block)
    {
        var table = Table;
        var circularIndex = blockIndex % table.Length;
        block = table[circularIndex];
        if (block == null)
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
        Array.Fill(Table, null); 
        LastReplacementTicks.Clear();
    }

    public DecompressedBlock[] ToArray()
    {
        return Table.Where(x => x != null).ToArray();
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

