using Tenray.Collections;
using Tenray.Segments;
using ZoneTree.Collections.TimSort;
using ZoneTree.Core;
using ZoneTree.WAL;

namespace Tenray;

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