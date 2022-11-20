namespace Tenray.ZoneTree.WAL;

public sealed class SyncCompressedModeOptions
{
    /// <summary>
    /// In sync-compressed WAL mode, if enabled
    /// a separate thread starts to write the tail block to the file
    /// periodically.
    /// This improves durability in compressed WALs.
    /// Default value is true.
    /// </summary>
    public bool EnableTailWriterJob { get; set; } = true;

    /// <summary>
    /// The delay in milliseconds before the next tail write.
    /// Default value is 500 ms.
    /// </summary>
    public int TailWriterJobInterval { get; set; } = 500;
}
