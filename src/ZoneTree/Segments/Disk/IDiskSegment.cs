using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.Disk;

public interface IDiskSegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>, IIndexedReader<TKey, TValue>, IDisposable
{
    /// <summary>
    /// Gets number of the records in the segment.
    /// </summary>
    new long Length { get; }

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
    /// Increments the iterator reader counter
    /// to ensure that disk segment stays alive 
    /// until all iterators call DetachIterator.
    /// </summary>
    void AttachIterator();

    /// <summary>
    /// Decrements the iterator reader counter. 
    /// When there is no attached iterator remaining and
    /// the drop is already requested,
    /// calls Drop().
    /// </summary>
    void DetachIterator();

    /// <summary>
    /// Release internal read buffers 
    /// that are not used after given ticks.
    /// </summary>
    /// <returns>Total released read buffer count.</returns>
    int ReleaseReadBuffers(long ticks);

    /// <summary>
    /// Returns the first keys of every part.
    /// </summary>
    /// <returns>Keys</returns>
    TKey[] GetFirstKeysOfEveryPart();

    /// <summary>
    /// Returns the last keys of every part.
    /// </summary>
    /// <returns>Keys</returns>
    TKey[] GetLastKeysOfEveryPart();

    /// <summary>
    /// Returns the last values of every part.
    /// </summary>
    /// <returns>Values</returns>
    TValue[] GetLastValuesOfEveryPart();

    /// <summary>
    /// Exceptions occurs in delayed drops (eg: iterators delays segment drops)
    /// are being reported to the IZoneTreeMaintenance interface events 
    /// through this delegate.
    /// This is for internal usage.
    /// </summary>
    internal Action<IDiskSegment<TKey, TValue>, Exception> DropFailureReporter { get; set; }

    /// <summary>
    /// Gets part.
    /// </summary>
    /// <param name="partIndex"></param>
    /// <returns></returns>
    IDiskSegment<TKey, TValue> GetPart(int partIndex);

    /// <summary>
    /// Drops all sectors excluding given exclusion list.
    /// </summary>
    /// <param name="excludedPartIds"></param>
    void Drop(HashSet<long> excludedPartIds);
}
