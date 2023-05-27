using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Segments;

public sealed class ReadOnlySegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public ReadOnlySegmentLoader(
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IReadOnlySegment<TKey, TValue> LoadReadOnlySegment(long segmentId)
    {
        var wal = Options.WriteAheadLogProvider.GetOrCreateWAL(
            segmentId,
            ZoneTree<TKey, TValue>.SegmentWalCategory,
            Options.WriteAheadLogOptions,
            Options.KeySerializer,
            Options.ValueSerializer);
        var result = wal.ReadLogEntries(false, false, true);
        if (!result.Success)
        {
            if (result.HasFoundIncompleteTailRecord)
            {
                var incompleteTailException = result.IncompleteTailRecord;
                wal.TruncateIncompleteTailRecord(incompleteTailException);
            }
            else
            {
                Options.WriteAheadLogProvider.RemoveWAL(
                    segmentId,
                    ZoneTree<TKey, TValue>.SegmentWalCategory);
                using var disposeWal = wal;
                throw new WriteAheadLogCorruptionException(segmentId, result.Exceptions);
            }
        }
        wal.MarkFrozen();
        Options.WriteAheadLogProvider.RemoveWAL(
            segmentId,
            ZoneTree<TKey, TValue>.SegmentWalCategory);

        (var newKeys, var newValues) = WriteAheadLogUtility
            .StableSortAndCleanUpDeletedKeys(
            result.Keys,
            result.Values,
            Options.Comparer,
            Options.IsValueDeleted);

        var ros = new ReadOnlySegment<TKey, TValue>(
            segmentId,
            Options,
            newKeys,
            newValues)
        {
            MaximumOpIndex = result.MaximumOpIndex
        };
        return ros;
    }
}