namespace Tenray.ZoneTree.WAL;

public sealed class CompressedImmediateModeOptions
{
    /// <summary>
    /// In compressed WAL mode, if enabled
    /// a separate thread started to write the tail block to the file
    /// periodically.
    /// This improves durability in compressed WALs.
    /// </summary>
    public bool EnableTailWriterJob { get; set; } = true;

    /// <summary>
    /// The delay in milliseconds before the next tail write.
    /// </summary>
    public int TailWriterJobInterval { get; set; } = 100;
}
