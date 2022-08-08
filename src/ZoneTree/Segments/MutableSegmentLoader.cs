﻿using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Segments;

public class MutableSegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public MutableSegmentLoader(
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IMutableSegment<TKey, TValue> LoadMutableSegment(long segmentId)
    {
        var wal = Options.WriteAheadLogProvider
            .GetOrCreateWAL(
                segmentId,
                ZoneTree<TKey,TValue>.SegmentWalCategory,
                Options.KeySerializer, Options.ValueSerializer);
        var result = wal.ReadLogEntries(false, false);
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
        return new MutableSegment<TKey, TValue>(segmentId, wal, Options, result.Keys, result.Values);
    }
}
