namespace Tenray.ZoneTree.WAL;

public enum WriteAheadLogMode
{
    /// <summary>
    /// Immediate mode write ahead log ensures that the data is flushed to the device
    /// immediately.
    /// It provides maximum durability in case of a crash/power cut,
    /// but slower write speed.
    /// </summary>
    Immediate,

    /// <summary>
    /// Lazy mode write ahead log does not directly write and flush
    /// the log entries to the device. Entries are kept in memory and
    /// written to the device in another thread.
    /// Lazy mode improves the performance with the cost of reduced durability 
    /// (crashes might cause data loss.)
    /// Lazy mode writes to the WAL with compression enabled.
    /// </summary>
    Lazy,

    /// <summary>
    /// Immediate mode with compression.
    /// It provides faster write speed but less durability.
    /// (crashes might cause data loss.)
    /// </summary>
    CompressedImmediate,

    /// <summary>
    /// No Write Ahead Log. Nothing is saved to the WAL file.
    /// Every inserts stay in memory. Data in memory can disappear on 
    /// process crashes / terminations or power cuts.
    /// It is still possible to save in memory data to the disk segments
    /// using MoveSegmentZeroForward and StartMergeOperation
    /// methods in IZoneTreeMaintenance.
    /// </summary>
    None,
}