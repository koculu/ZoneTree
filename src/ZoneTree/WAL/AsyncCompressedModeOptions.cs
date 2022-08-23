namespace Tenray.ZoneTree.WAL;

public sealed class AsyncCompressedModeOptions
{
    /// <summary>
    /// The delay in milliseconds before making the next poll
    /// to retrieve the new entries in the queue,
    /// when the queue is empty.
    /// </summary>
    public int EmptyQueuePollInterval { get; set; } = 100;
}
