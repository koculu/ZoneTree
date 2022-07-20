using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments;

public class ReadOnlySegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public ReadOnlySegmentLoader(
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IReadOnlySegment<TKey, TValue> LoadReadOnlySegment(int segmentId)
    {
        var wal = Options.WriteAheadLogProvider.GetOrCreateWAL(
            segmentId,
            Options.KeySerializer,
            Options.ValueSerializer);
        var result = wal.ReadLogEntries(false, false);

        if (!result.Success)
        {
            Options.WriteAheadLogProvider.RemoveWAL(segmentId);
            using var disposeWal = wal;
            throw new WriteAheadLogCorruptionException(segmentId, result.Exceptions);
        }

        (var newKeys, var newValues) = WriteAheadLogUtility.StableSortAndCleanUpDeletedKeys(
            result.Keys,
            result.Values,
            Options.Comparer,
            Options.IsValueDeleted);

        return new ReadOnlySegment<TKey, TValue>(
            segmentId,
            Options,
            newKeys,
            newValues);
    }
}