using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.WAL;

public interface IWriteAheadLogProvider
{
    WriteAheadLogMode WriteAheadLogMode { get; set; }

    /// <summary>
    /// In compressed WAL mode, if enabled
    /// a separate thread started to write the tail block to the file
    /// periodically.
    /// This improves durability in compressed WALs.
    /// </summary>
    bool EnableTailWriterJob { get; set; }

    /// <summary>
    /// The delay in milliseconds before the next tail write.
    /// </summary>
    int TailWriterJobInterval { get; set; }

    /// <summary>
    /// Incremental backup is a WAL feature which moves
    /// all WAL data to another incremental log file.
    /// It is required to compact WAL in memory without data loss in 
    /// persistent device.
    /// </summary>
    bool EnableIncrementalBackup { get; set; }

    void InitCategory(string category);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId,
        string category,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category);

    bool RemoveWAL(long segmentId, string category);

    void DropStore();
}
