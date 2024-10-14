namespace Tenray.ZoneTree.Core;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Tenray.ZoneTree.Collections;

public sealed class Replicator<TKey, TValue> : IDisposable
{
    readonly IZoneTree<TKey, TValue> Replica;

    readonly IZoneTree<TKey, long> LatestOpIndexes;

    readonly IMaintainer Maintainer;

    bool isDisposed;

    public Replicator(
        IZoneTree<TKey, TValue> replica,
        string dataPath,
        Action<ZoneTreeFactory<TKey, long>> configure = null)
    {
        this.Replica = replica;
        var factory = new ZoneTreeFactory<TKey, long>()
            .SetDataDirectory(dataPath);
        if (configure != null) configure(factory);
        LatestOpIndexes = factory.OpenOrCreate();
        Maintainer = LatestOpIndexes.CreateMaintainer();
        Maintainer.EnableJobForCleaningInactiveCaches = true;
    }

    public void OnUpsert(TKey key, TValue value, long opIndex)
    {
        LatestOpIndexes.TryAtomicAddOrUpdate(
                key,
                (ref long newOpIndex) =>
                {
                    newOpIndex = opIndex;
                    return true;
                },
                (ref long existingOpIndex) =>
                {
                    if (opIndex < existingOpIndex)
                        return false;
                    existingOpIndex = opIndex;
                    return true;
                },
                (in long _, long _, OperationResult result) =>
                {
                    if (result == OperationResult.Cancelled) return;
                    Replica.Upsert(key, value);
                });
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        Maintainer.EvictToDisk();
        Maintainer.WaitForBackgroundThreads();
        Maintainer.Dispose();
        LatestOpIndexes.Dispose();
    }
}
