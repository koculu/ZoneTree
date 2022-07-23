namespace Tenray.ZoneTree.WAL;

public enum WriteAheadLogMode
{
    /// <summary>
    /// Immediate mode write ahead log ensures the written data flushed immediately.
    /// This provides best data consistency in case of a crash.
    /// </summary>
    Immediate,

    /// <summary>
    /// Lazy mode write ahead log does not directly writes and flushed 
    /// the log entries to the device.
    /// Lazy mode improves the performance with the cost of reduced durability.
    /// </summary>
    Lazy
}