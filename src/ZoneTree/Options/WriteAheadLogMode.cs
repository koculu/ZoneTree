namespace Tenray.ZoneTree.Options;

/// <summary>
/// Available write ahead log modes.
/// </summary>
public enum WriteAheadLogMode : byte
{
    /// <summary>
    /// Sync mode write ahead log ensures that the data is flushed to the device
    /// immediately.
    /// It provides maximum durability in case of a crash/power cut,
    /// but slower write speed.
    /// </summary>
    Sync = 0,

    /// <summary>
    /// Sync mode with compression.
    /// It provides faster write speed but less durability.
    /// (crashes might cause data loss.)
    /// </summary>
    SyncCompressed = 1,

    /// <summary>
    /// AsyncCompressed mode write ahead log does not directly write and flush
    /// the log entries to the device. Entries are kept in memory and
    /// written to the device in another thread.
    /// AsyncCompressed mode improves the performance with the cost of reduced durability 
    /// (crashes might cause data loss.)
    /// AsyncCompressed mode writes to the WAL with compression enabled.
    /// </summary>
    AsyncCompressed = 2,

    /// <summary>
    /// No Write Ahead Log. Nothing is saved to the WAL file.
    /// Every inserts stay in memory. Data in memory can disappear on 
    /// process crashes / terminations or power cuts.
    /// It is still possible to save in memory data to the disk segments
    /// using MoveMutableSegmentForward and StartMergeOperation
    /// methods in IZoneTreeMaintenance.
    /// </summary>
    None = 3,
}