using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.Disk;

public interface IDiskSegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>, IIndexedReader<TKey, TValue>, IDisposable
{
    /// <summary>
    /// Gets number of the records in the segment.
    /// </summary>
    new int Length { get; }

    /// <summary>
    /// Returns read buffer count.
    /// </summary>
    int ReadBufferCount { get; }

    /// <summary>
    /// Inits sparse array.
    /// </summary>
    /// <param name="size"></param>
    void InitSparseArray(int size);

    /// <summary>
    /// Initialize a sparse array alligned with segment length,
    /// This enables faster reads without IO.
    /// </summary>
    void LoadIntoMemory();

    /// <summary>
    /// Increments the reader counter to ensure that disk segment stays alive 
    /// until all readers call remove reader.
    /// </summary>
    void AddReader();

    /// <summary>
    /// Decreases the reader counter.
    /// </summary>
    void RemoveReader();

    /// <summary>
    /// Release internal read buffers 
    /// that are not used after given ticks.
    /// </summary>
    /// <returns>Total released read buffer count.</returns>
    int ReleaseReadBuffers(long ticks);

    /// <summary>
    /// Exceptions occurs in delayed drops (eg: iterators delays segment drops)
    /// are being reported to the IZoneTreeMaintenance interface events 
    /// through this delegate.
    /// This is for internal usage.
    /// </summary>
    public Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }
}
