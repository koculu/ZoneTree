using Microsoft.Extensions.Logging;

namespace Tenray.ZoneTree;

public interface IZoneTree<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Checks the existence of the key in the tree.
    /// </summary>
    /// <param name="key">The key of the element.</param>
    /// <returns>true if key is found in tree; otherwise, false</returns>
    bool ContainsKey(in TKey key);

    /// <summary>
    /// Tries to get the value of the given key.
    /// </summary>
    /// <param name="key">The key of the element.</param>
    /// <param name="value">The value of the element associated with the key.</param>
    /// <returns>true if the key is found; otherwise, false</returns>
    bool TryGet(in TKey key, out TValue value);

    /// <summary>
    /// Attempts to add the specified key and value atomically across LSM-Tree segments.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. It can be null.</param>
    /// <returns>true if the key/value pair was added successfully;
    /// otherwise, false.</returns>
    bool TryAtomicAdd(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to update the specified key's value atomically across LSM-Tree segments.
    /// </summary>
    /// <param name="key">The key of the element to update.</param>
    /// <param name="value">The value of the element to update. It can be null.</param>
    /// <returns>true if the key/value pair was updated successfully;
    /// otherwise, false.</returns>
    bool TryAtomicUpdate(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to add or update the specified key and value atomically across LSM-Tree segments.    
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueToAdd">The value of the element to add. It can be null.</param>
    /// <param name="valueProviderToUpdate">The lambda function that returns value to be updated.</param>
    /// <returns>true if the key/value pair was added;
    /// false, if the key/value pair was updated.</returns>
    bool TryAtomicAddOrUpdate(in TKey key, in TValue valueToAdd, Func<TValue, TValue> valueProviderToUpdate);

    /// <summary>
    /// Adds or updates the specified key/value pair atomically across LSM-Tree segments.
    /// </summary>
    /// <param name="key">The key of the element to upsert.</param>
    /// <param name="value">The value of the element to upsert.</param>
    void AtomicUpsert(in TKey key, in TValue value);

    /// <summary>
    /// Adds or updates the specified key/value pair.
    /// </summary>
    /// <remarks>
    /// This is a thread-safe method, but it is not sycnhronized 
    /// with other atomic add/update/upsert methods.
    /// Using the Upsert method in parallel to atomic methods breaks the atomicity
    /// of the atomic methods.
    /// 
    /// For example: 
    /// TryAtomicAddOrUpdate(key) does the following 3 things
    /// within lock to preserve atomicity across segments of LSM-Tree.
    ///  1. tries to get the value of the key
    ///  2. if it can find the key, it updates the value
    ///  3. if it cannot find the key, inserts the new key/value
    ///  
    /// All atomic methods respect this order using the same lock.
    /// 
    /// The Upsert method does not respect to the atomicity of atomic methods,
    /// because it does upsert without lock.
    /// 
    /// On the other hand,
    /// the Upsert method is atomic in the mutable segment scope but not across all segments.
    /// This makes Upsert method thread-safe.
    /// 
    /// This is the fastest add or update function.
    /// </remarks>    
    /// <param name="key">The key of the element to upsert.</param>
    /// <param name="value">The value of the element to upsert.</param>
    void Upsert(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to delete the specified key.
    /// </summary>
    /// <param name="key">The key of the element to delete.</param>
    /// <returns>true if the key was found and deleted;
    /// false if the key was not found.</returns>
    bool TryDelete(in TKey key);

    /// <summary>
    /// Deletes the specified key regardless of existence. (hint: LSM Tree delete is an insert)
    /// This is faster than TryDelete because it does not check existence in all layers.
    /// It increases the data lake size.
    /// </summary>
    /// <param name="key">The key of the element to delete.</param>
    void ForceDelete(in TKey key);

    /// <summary>
    /// Counts Keys in the entire database.
    /// This operation requires a scan through in memory segments and lookups in disk segment.
    /// </summary>
    /// <returns>Number of the valid records in the tree.</returns>
    int Count();

    /// <summary>
    /// Creates an iterator that enables scanning of the entire database.
    /// </summary>
    /// 
    /// <remarks>
    /// The iterator might or might not retrieve newly inserted elements.
    /// This depends on the iterator's internal segment iterator positions.
    /// 
    /// If the newly inserted or deleted key is after the internal segment iterator position,
    /// the new data is included in the iteration.
    /// 
    /// Iterators are lightweight.
    /// Create them when you need and dispose them when you dont need.
    /// Iterators acquire locks on the disk segment and prevents its disposal.
    /// 
    /// Use snapshot iterators for consistent view by ignoring new writes.
    /// </remarks>
    /// 
    /// <param name="iteratorType">Defines iterator type.</param>
    /// <param name="includeDeletedRecords">if true the iterator retrieves 
    /// the deleted and normal records</param>
    /// 
    /// <returns>ZoneTree Iterator</returns>
    IZoneTreeIterator<TKey, TValue> CreateIterator(
        IteratorType iteratorType = IteratorType.AutoRefresh,
        bool includeDeletedRecords = false);

    /// <summary>
    /// Creates a reverse iterator that enables scanning of the entire database.
    /// </summary>
    /// 
    /// <remarks>
    /// ZoneTree iterator direction does not hurt performance.
    /// Forward and backward iterator's performances are equal.
    /// </remarks>
    /// 
    /// <param name="iteratorType">Defines iterator type.</param>/// <param name="includeDeletedRecords">if true the iterator retrieves 
    /// the deleted and normal records</param>
    /// 
    /// <returns>ZoneTree Iterator</returns>
    IZoneTreeIterator<TKey, TValue> CreateReverseIterator(
        IteratorType iteratorType = IteratorType.AutoRefresh,
        bool includeDeletedRecords = false);

    /// <summary>
    /// Returns maintenance object belongs to this ZoneTree.
    /// </summary>
    IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    /// <summary>
    /// Enables read-only mode.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// ZoneTree Logger.
    /// </summary>
    public ILogger Logger { get; }
}
