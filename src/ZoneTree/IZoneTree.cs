using ZoneTree;

namespace Tenray;

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
    /// Attempts to add the specified key and value.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. It can be null.</param>
    /// <returns>true if the key/value pair was added successfully;
    /// otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    bool TryAdd(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to update the specified key's value.
    /// </summary>
    /// <param name="key">The key of the element to update.</param>
    /// <param name="value">The value of the element to update. It can be null.</param>
    /// <returns>true if the key/value pair was updated successfully;
    /// otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    bool TryUpdate(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to add or update the specified key and value using atomic updater.        
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueToAdd">The value of the element to add. It can be null.</param>
    /// <param name="valueProviderToUpdate">The lambda function that returns value to be updated.</param>
    /// <returns>true if the key/value pair was added;
    /// false, if the key/value pair was updated.</returns>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    bool TryAddOrUpdateAtomic(in TKey key, in TValue valueToAdd, Func<TValue, TValue> valueProviderToUpdate);

    /// <summary>
    /// Adds or updates the specified key/value pair.
    /// </summary>
    /// <param name="key">The key of the element to upsert.</param>
    /// <param name="value">The value of the element to upsert. It can be null.</param>
    /// <returns>true if the key/value pair was inserted;
    /// false if the key/value pair was updated.</returns>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    bool Upsert(in TKey key, in TValue value);

    /// <summary>
    /// Attempts to delete the specified key.
    /// </summary>
    /// <param name="key">The key of the element to delete.</param>
    /// <returns>true if the key was found and deleted;
    /// false if the key was not found.</returns>
    /// <exception cref="ArgumentNullException">key is null.</exception>
    bool TryDelete(in TKey key);

    /// <summary>
    /// Deletes the specified key regardless of existence. (hint: LSM Tree delete is an insert)
    /// This is faster than TryDelete because it does not check existence in all layers.
    /// It increases the data lake size.
    /// </summary>
    /// <param name="key">The key of the element to delete.</param>
    /// <exception cref="ArgumentNullException">key is null.</exception>
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
    /// <remarks>
    /// The iterator might or might not retrieve newly inserted elements.
    /// This depends on the iterator's internal segment iterator positions.
    /// If the newly inserted or deleted key is after the internal segment iterator position,
    /// the new data is included in the iteration.
    /// Also, if there happens segment movement internally after iterators are created,
    /// new mutable segment is completely ignored and the iterator acts like a snapshot iterator.
    /// To refresh the iterator you need to create a new one.
    /// Iterators are cheap. Create new iterators when you need them and
    /// dispose the iterator when you are done with them.
    /// </remarks>
    /// <returns>ZoneTree Iterator</returns>
    IZoneTreeIterator<TKey, TValue> CreateIterator();

    /// <summary>
    /// Creates a reverse iterator that enables scanning of the entire database.
    /// </summary>
    /// <remarks>
    /// ZoneTree iterator direction does not hurt performance.
    /// Forward and backward iterator's performances are equal.
    /// </remarks>
    /// <returns>ZoneTree Iterator</returns>
    IZoneTreeIterator<TKey, TValue> CreateReverseIterator();

    /// <summary>
    /// Returns maintenance object belongs to this ZoneTree.
    /// </summary>
    IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }
}
