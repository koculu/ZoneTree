namespace Tenray.ZoneTree.WAL;

public enum WriteAheadLogMode
{
    /// <summary>
    /// Immediate mode write ahead log ensures that the data is flushed to the device
    /// immediately.
    /// This provides maximum durability in case of a crash/power cut.
    /// </summary>
    Immediate,

    /// <summary>
    /// Lazy mode write ahead log does not directly write and flush
    /// the log entries to the device. Entries are kept in memory and
    /// written to the device in another thread.
    /// Lazy mode improves the performance with the cost of reduced durability 
    /// (crashes might cause data loss.)
    /// Lazy mode writes to the WAL with compression.
    /// </summary>
    Lazy,

    /// <summary>
    /// Immediate mode with compression.
    /// Compression requires chunks and reduces the durability in case of a crash/power cut.
    /// Use Immediate mode for maximum durability.
    /// </summary>
    CompressedImmediate
}