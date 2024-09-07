using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Segments;

public interface IMutableSegment<TKey, TValue> : IReadOnlySegment<TKey, TValue>
{
    /// <summary>
    /// Frozen segments prevents new write requests.
    /// It is the transition stage before moving to read only layer.
    /// </summary>
    bool IsFrozen { get; }

    AddOrUpdateResult Upsert(in TKey key, in TValue value, out long opIndex);

    AddOrUpdateResult Delete(in TKey key, out long opIndex);

    void Freeze();

    IIncrementalIdProvider OpIndexProvider { get; }
}
