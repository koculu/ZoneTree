namespace Tenray.ZoneTree;

/// <summary>
/// The ZoneTree iterator interface.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public interface IZoneTreeIterator<TKey, TValue> : IDisposable
{
    /// <summary>
    /// Returns the element key if there is an element in the iterator position.
    /// </summary>
    TKey CurrentKey { get; }

    /// <summary>
    /// Returns the element value if there is an element in the iterator position.
    /// </summary>
    TValue CurrentValue { get; }

    /// <summary>
    /// Returns true if there is an element in the iterator position.
    /// </summary>
    bool HasCurrent { get; }

    /// <summary>
    /// If true, the iterator automatically refreshes itself to include
    /// the latest segments.
    /// </summary>
    bool AutoRefresh { get; }

    /// <summary>
    /// Gets the current element at the iterator's position.
    /// </summary>
    KeyValuePair<TKey, TValue> Current { get; }

    /// <summary>
    /// Iterates to the next element.
    /// </summary>
    /// <returns>true if next element exists, otherwise false.</returns>
    bool Next();

    /// <summary>
    /// Seeks the iterator to the position where next item is the key.
    /// If key does not exist,
    /// forward iterator's next item is the first greater item (prefix search forward).
    /// reverse iterator's next item is the last smaller item (prefix search backward).
    /// Complexity: O(log(N))
    /// </summary>
    /// <param name="key">The search key</param>
    void Seek(in TKey key);

    /// <summary>
    /// Seeks the first element of the iterator.
    /// </summary>
    void SeekFirst();

    /// <summary>
    /// Refreshes the iterator with latest segments. 
    /// If AutoRefresh property is true, there is no need to call refresh manually.
    /// </summary>
    void Refresh();
}
