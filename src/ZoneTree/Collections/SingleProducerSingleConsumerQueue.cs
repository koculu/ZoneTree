using System.Collections;

namespace Tenray.ZoneTree.Collections;

/// <summary>
/// Special Queue for ZoneTree.
/// 1. SingleProducerSingleConsumerQueue is 
///   - thread-safe for single producer and single consumer.
///   - thread safe for many readers / enumerations
/// 2. enquue method uses lock when it is full which makes it almost lock-free for inserts.
/// 3. dequeue uses lock but the producer almost never hit the lock.
/// 4. Despite this is a FIFO Queue, the enumerator is in LIFO order 
/// to optimize record lookup at TryGetFromReadonlySegments.
/// Enqueue/Dequeue items in FIFO order: i1,i2,i3,i4
/// Enumeration in LIFO order: i4,i3,i2,i1
/// </summary>
/// <typeparam name="TQueueItem">Type of the queue item.</typeparam>
public sealed class SingleProducerSingleConsumerQueue<TQueueItem>
    : IEnumerable<TQueueItem>
    where TQueueItem : class
{
    class QueueItemsChunk
    {
        const int ChunkSize = 16;

        /// <summary>
        /// Start of the queue inclusive.
        /// </summary>
        public volatile int Start = 0;

        /// <summary>
        /// End of the queue exclusive.
        /// </summary>
        public volatile int End = 0;

        public volatile TQueueItem[] Items;

        public bool IsEmpty => Start == End;

        public int ItemsCount
        {
            get
            {
                var size = Items.Length;
                return (End + size - Start) % size;
            }
        }

        public QueueItemsChunk()
        {
            Items = new TQueueItem[ChunkSize];
        }

        public QueueItemsChunk(TQueueItem[] items, int start, int end)
        {
            Items = items;
            Start = start;
            End = end;
        }

        public IReadOnlyList<TQueueItem> ToFirstInFirstArray()
        {
            var items = Items;
            var size = items.Length;
            var end = End;
            var start = Start;
            var list = new List<TQueueItem>(ItemsCount);

            while (start != end)
            {
                var item = items[start];
                if (item == null)
                    continue;
                list.Add(item);
                start = (start + 1) % size;
            }
            return list;
        }

        public IReadOnlyList<TQueueItem> ToLastInFirstArray()
        {
            var items = Items;
            var size = items.Length;
            var end = (size + End - 1) % size;
            var start = (size + Start - 1) % size;
            var list = new List<TQueueItem>(ItemsCount);

            while (start != end)
            {
                var item = items[end];
                if (item == null)
                    continue;
                list.Add(item);
                end = (size + end - 1) % size;
            }
            return list;
        }
    }

    public int Length => Chunk.ItemsCount;

    public bool IsEmpty => Chunk.IsEmpty;

    volatile QueueItemsChunk Chunk = new();

    public SingleProducerSingleConsumerQueue()
    {
    }

    public SingleProducerSingleConsumerQueue(IEnumerable<TQueueItem> list)
    {
        foreach (var item in list)
        {
            Enqueue(item);
        }
    }

    /// <summary>
    /// Enqueue should not be called more than once at the same time.
    /// </summary>
    public void Enqueue(TQueueItem item)
    {
        var chunk = Chunk;
        var items = chunk.Items;
        var size = items.Length;
        var end = chunk.End;
        if ((end + 1) % size == chunk.Start)
        {
            // queue is full or was full.
            // lock frequency of enqueue is almost zero due to the exponential size increase.
            lock (this)
            {
                var newItems = new TQueueItem[size * 2];
                Array.Copy(items, newItems, size);
                if (end < chunk.Start)
                {
                    Array.Copy(items, 0, newItems, size, end);
                    Array.Fill(newItems, null, 0, end);
                    end = size + chunk.End;
                }
                chunk = Chunk = new QueueItemsChunk(newItems, chunk.Start, end);
                items = newItems;
            }
            size *= 2;
        }
        items[end] = item;
        chunk.End = (end + 1) % size;
    }

    /// <summary>
    /// TryDequeue should not be called more than once at the same time.
    /// </summary>
    public bool TryDequeue(out TQueueItem item)
    {
        var chunk = Chunk;
        var start = chunk.Start;
        var items = chunk.Items;
        var size = items.Length;
        item = items[start];
        if (item == null)
            return false;

        lock (this)
        {
            if (!ReferenceEquals(chunk, Chunk))
            {
                chunk = Chunk;
                items = chunk.Items;
                size = items.Length;
            }
            items[start] = null;
            chunk.Start = (start + 1) % size;
        }
        return true;
    }

    public IReadOnlyList<TQueueItem> ToLastInFirstArray() => Chunk.ToLastInFirstArray();

    public IReadOnlyList<TQueueItem> ToFirstInFirstArray() => Chunk.ToFirstInFirstArray();

    class LastInFirstEnumerator : IEnumerator<TQueueItem>
    {
        TQueueItem current;

        public TQueueItem Current => current;

        object IEnumerator.Current => current;

        readonly QueueItemsChunk Chunk;

        TQueueItem[] Items;

        int Start;

        int End;

        int Size;

        public LastInFirstEnumerator(QueueItemsChunk chunk)
        {
            Chunk = chunk;
            Reset();
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// The enumeration of this quewue is LIFO.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            do
            {
                if (Start == End)
                    return false;
                current = Items[End];
                End = (Size + End - 1) % Size;
            }
            while (current == null);
            return true;
        }

        public void Reset()
        {
            var chunk = Chunk;
            Items = chunk.Items;
            Size = Items.Length;
            Start = (Size + chunk.Start - 1) % Size;
            End = (Size + chunk.End - 1) % Size;
        }
    }

    public IEnumerator<TQueueItem> GetEnumerator()
    {
        return new LastInFirstEnumerator(Chunk);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new LastInFirstEnumerator(Chunk);
    }
}