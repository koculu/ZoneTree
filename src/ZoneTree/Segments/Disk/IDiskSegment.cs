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
    /// Returns the first keys of every sector.
    /// </summary>
    /// <returns>Keys</returns>
    TKey[] GetFirstKeysOfEverySector();

    /// <summary>
    /// Returns the last keys of every sector.
    /// </summary>
    /// <returns>Keys</returns>
    TKey[] GetLastKeysOfEverySector();

    /// <summary>
    /// Returns the last values of every sector.
    /// </summary>
    /// <returns>Values</returns>
    TValue[] GetLastValuesOfEverySector();

    /// <summary>
    /// Exceptions occurs in delayed drops (eg: iterators delays segment drops)
    /// are being reported to the IZoneTreeMaintenance interface events 
    /// through this delegate.
    /// This is for internal usage.
    /// </summary>
    internal Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    /// <summary>
    /// Gets sector.
    /// </summary>
    /// <param name="sectorIndex"></param>
    /// <returns></returns>
    IDiskSegment<TKey, TValue> GetSector(int sectorIndex);

    /// <summary>
    /// Drops all sectos excluding given exclusion list.
    /// </summary>
    /// <param name="excludedSectorIds"></param>
    void Drop(HashSet<int> excludedSectorIds);
}
