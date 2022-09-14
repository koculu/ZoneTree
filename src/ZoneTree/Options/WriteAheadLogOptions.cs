using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Options;

/// <summary>
/// Write Ahead Log Options. The options will be used
/// for creation of new Write Ahead Logs.
/// Existing WALs will be created with their existing options.
/// </summary>
public sealed class WriteAheadLogOptions
{
    /// <summary>
    /// The default write ahead log mode. New WALs will be created
    /// based on this setting. Default value is AsyncCompressed.
    /// </summary>
    public WriteAheadLogMode WriteAheadLogMode { get; set; }
        = WriteAheadLogMode.AsyncCompressed;

    /// <summary>
    /// The custom options are reserved to be used
    /// by custom write ahead log providers.
    /// </summary>
    public object CustomOptions { get; set; }

    /// <summary>
    /// WAL compressin block size. New WALs will be created 
    /// based on this setting. Default value = 256 KB
    /// </summary>
    public int CompressionBlockSize { get; set; }
        = 1024 * 32 * 8;

    /// <summary>
    /// The compression method for the WALs with compression enabled.
    /// Default is LZ4.
    /// </summary>
    public CompressionMethod CompressionMethod { get; set; } = CompressionMethod.LZ4;

    /// <summary>
    /// The compression level of the selected compression method.
    /// Default is <see cref="CompressionLevels.LZ4Fastest"/>.
    /// </summary>
    public int CompressionLevel { get; set; } = CompressionLevels.LZ4Fastest;

    /// <summary>
    /// Options for sync-compressed mode.
    /// </summary>
    public SyncCompressedModeOptions SyncCompressedModeOptions { get; set; } = new();

    /// <summary>
    /// Options for async-compressed mode.
    /// </summary>
    public AsyncCompressedModeOptions AsyncCompressedModeOptions { get; set; } = new();

    /// <summary>
    /// Incremental backup is a WAL feature which moves
    /// all WAL data to another incremental log file when it is compacted.
    /// It is required to compact WAL in memory without data loss in 
    /// persistent device. Used by Optimistic Transactional ZoneTree for
    /// transaction log compaction. Enabling backup will make transactions 
    /// slower.
    /// Default value is false.
    /// </summary>
    public bool EnableIncrementalBackup { get; set; }
}
