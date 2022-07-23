namespace Tenray.ZoneTree.WAL;

public enum WriteAheadLogMode
{
    /// <summary>
    /// Immediate mode write ahead log ensures that the data is flushed to the device
    /// immediately.
    /// This provides best data consistency in case of a crash.
    /// </summary>
    Immediate,

    /// <summary>
    /// Lazy mode write ahead log does not directly write and flush
    /// the log entries to the device. Entries are kept in memory and
    /// written to the device in another thread.
    /// Lazy mode improves the performance with the cost of reduced durability 
    /// (crashes might cause data loss.)
    /// </summary>
    Lazy
}