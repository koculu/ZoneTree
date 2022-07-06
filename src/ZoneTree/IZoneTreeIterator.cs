namespace ZoneTree;

public interface IZoneTreeIterator<TKey, TValue> : IDisposable
{
    TKey CurrentKey { get; }

    TValue CurrentValue { get; }

    bool HasCurrent { get; }

    KeyValuePair<TKey, TValue> Current { get; }

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

    void Reset();
}
