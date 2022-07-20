using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments;

public interface IMutableSegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>
{
    /// <summary>
    /// Frozen segments prevents new write requests.
    /// It is the transition stage before moving to read only layer.
    /// </summary>
    bool IsFrozen { get; }

    AddOrUpdateResult Upsert(TKey key, TValue value);

    AddOrUpdateResult Delete(TKey key);

    IReadOnlySegment<TKey, TValue> CreateReadOnlySegment();

    void Freeze();
}
