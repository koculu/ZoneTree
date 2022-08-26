namespace Tenray.ZoneTree.Options;

/// <summary>
/// Available compression methods.
/// </summary>
public enum CompressionMethod : byte
{
    /// <summary>
    /// No compression.
    /// </summary>
    None = 0,

    /// <summary>
    /// Gzip algorithm.
    /// </summary>
    Gzip = 1,

    /// <summary>
    /// LZ4 algorithm.
    /// </summary>
    LZ4 = 2,

    /// <summary>
    /// ZStd algorithm.
    /// </summary>
    Zstd = 3,

    /// <summary>
    /// Brotli algorithm.
    /// </summary>
    Brotli = 4,
}
