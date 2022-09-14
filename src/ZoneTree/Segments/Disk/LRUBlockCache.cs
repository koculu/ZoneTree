namespace Tenray.ZoneTree.Segments.Disk;

/// <summary>
/// Thread-safe LRU Block Cache.
/// Note: This is slower than CircularBlockCache.
/// Hence it is not being used.
/// LRUBlockCache stays in the codebase for further analysis on topic.
/// </summary>
public sealed class LRUBlockCache
{
    readonly int Capacity;

    readonly Dictionary<int,
        LinkedListNode<DecompressedBlock>> Cache;

    readonly LinkedList<DecompressedBlock> List;
    
    volatile int _count;
    public int Count { get => _count; private set => _count = value; }

    public LRUBlockCache(int capacity)
    {
        Capacity = capacity;
        Cache = new(capacity);
        List = new();
    }

    public bool TryGetBlock(int blockIndex, out DecompressedBlock block)
    {
        lock (this)
        {
            if (!Cache.TryGetValue(blockIndex, out var node))
            {
                block = null;
                return false;
            }

            List.Remove(node);
            List.AddFirst(node);

            block = node.Value;
            return true;
        }
    }

    public void AddBlock(DecompressedBlock block)
    {
        lock (this)
        {
            var blockIndex = block.BlockIndex;
            if (Cache.TryGetValue(blockIndex, out var node))
            {
                List.Remove(node);
                List.AddFirst(node);
            }
            else
            {
                if (Cache.Count >= Capacity)
                {
                    var remove = List.Last.Value;
                    Cache.Remove(remove.BlockIndex);
                    List.RemoveLast();
                }
                else
                {
                    Cache.Add(blockIndex, List.AddFirst(block));
                    Count++;
                }
            }
        }
    }

    public void Clear()
    {
        lock (this)
        {
            Cache.Clear();
            List.Clear();
        }
    }

    public void RemoveBlock(int blockIndex)
    {
        lock (this)
        {
            if (Cache.TryGetValue(blockIndex, out var block))
            {
                Cache.Remove(blockIndex);
                List.Remove(block);
                --Count;
            }
        }
    }

    public DecompressedBlock[] ToArray()
    {
        lock (this)
        {
            return List.ToArray();
        }
    }
}