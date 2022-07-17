using Tenray.Segments;
using ZoneTree.Core;

namespace Tenray;

public class MutableSegmentLoader<TKey, TValue>
{
    public ZoneTreeOptions<TKey, TValue> Options { get; }

    public MutableSegmentLoader(
        ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
    }

    public IMutableSegment<TKey, TValue> LoadMutableSegment(int segmentId)
    {
        var wal = Options.WriteAheadLogProvider
            .GetOrCreateWAL(segmentId, Options.KeySerializer, Options.ValueSerializer);
        var result = wal.ReadLogEntries(false, false);
        if (!result.Success)
        {
            Options.WriteAheadLogProvider.RemoveWAL(segmentId);
            using var disposeWal = wal;
            throw new WriteAheadLogCorruptionException(segmentId, result.Exceptions);
        }
        return new MutableSegment<TKey, TValue>(segmentId, wal, Options, result.Keys, result.Values);
    }
}
