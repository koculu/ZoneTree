using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Segments;

public sealed class MutableSegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public MutableSegmentLoader(
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IMutableSegment<TKey, TValue> LoadMutableSegment(long segmentId, long maximumOpIndex)
    {
        var wal = Options.WriteAheadLogProvider
            .GetOrCreateWAL(
                segmentId,
                ZoneTree<TKey,TValue>.SegmentWalCategory,
                Options.WriteAheadLogOptions,
                Options.KeySerializer, Options.ValueSerializer);
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
        maximumOpIndex = Math.Max(result.MaximumOpIndex, maximumOpIndex);
        return new MutableSegment<TKey, TValue>
            (segmentId, wal, Options, result.Keys,
            result.Values, maximumOpIndex + 1);
    }
}
