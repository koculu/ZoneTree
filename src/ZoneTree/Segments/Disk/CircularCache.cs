using System;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class CircularCache<TDataType>
{
    public sealed class CacheAndCacheSize
    {
        public int CacheSize;
        public CachedRecord[] circularBuffer;
    }

    public sealed class CachedRecord
    {
        public long Index;
        public TDataType Record;
        public long LastAccess;
        public bool IsExpired(long ticks) => LastAccess < ticks;
    }

    CacheAndCacheSize Cache;

    public int RecordLifeTimeInMillisecond = 10000;

    int statsCacheHit = 0;

    int statsCacheMiss = 0;

    public (int cacheHit, int cacheMiss) GetCacheStats() => (statsCacheHit, statsCacheMiss);

    public void ResetCacheStats()
    {
        statsCacheHit = 0;
        statsCacheMiss = 0;
    }

    public CircularCache(int cacheSize, int recordLifeTimeInMillisecond)
    {
        RecordLifeTimeInMillisecond = recordLifeTimeInMillisecond;
        Cache = new CacheAndCacheSize()
        {
            CacheSize = cacheSize,
            circularBuffer = new CachedRecord[cacheSize]
        };
    }

    public void SetCacheSize(int cacheSize)
    {
        if (cacheSize < 0)
            cacheSize = 0;
        var newBuffer = new CachedRecord[cacheSize];
        var newCache = new CacheAndCacheSize
        {
            CacheSize = cacheSize,
            circularBuffer = newBuffer
        };
        if (cacheSize > 0)
        {
            var ticks = Environment.TickCount64 - RecordLifeTimeInMillisecond;
            var oldBuffer = Cache.circularBuffer;
            var len = oldBuffer.Length;
            for (int i = 0; i < len; i++)
            {
                var record = oldBuffer[i];
                var circularIndex = i % cacheSize;
                if (record.IsExpired(ticks)) continue;
                newBuffer[circularIndex] = record;
            }
        }
        Cache = newCache;
    }

    public bool TryGetFromCache(long index, out TDataType key)
    {
        var cache = Cache;
        var cacheSize = cache.CacheSize;
        if (cacheSize < 1)
        {
            key = default;
            return false;
        }
        var circularBuffer = cache.circularBuffer;
        var len = circularBuffer.Length;
        var circularIndex = index % cacheSize;
        var cacheRecord = circularBuffer[circularIndex];
        if (cacheRecord != null && cacheRecord.Index == index)
        {
            key = cacheRecord.Record;
            cacheRecord.LastAccess = Environment.TickCount64;
            ++statsCacheHit;
            return true;
        }
        ++statsCacheMiss;
        key = default;
        return false;
    }

    public bool TryAddToTheCache(long index, ref TDataType key)
    {
        var cache = Cache;
        var cacheSize = cache.CacheSize;
        if (cacheSize < 1) return false;
        var circularBuffer = cache.circularBuffer;
        var circularIndex = index % cacheSize;
        var existingCacheRecord = circularBuffer[circularIndex];
        /* Do not add a new cache record when the existing cache record is still active.*/
        if (existingCacheRecord != null &&
            !existingCacheRecord.IsExpired(Environment.TickCount64 - RecordLifeTimeInMillisecond))
        {
            return false;
        }
        var cachedRecord = new CachedRecord
        {
            Index = index,
            Record = key,
            LastAccess = Environment.TickCount64,
        };
        circularBuffer[circularIndex] = cachedRecord;
        return true;
    }

    public int ReleaseInactiveCacheRecords()
    {
        var ticks = Environment.TickCount64 - RecordLifeTimeInMillisecond;
        var circularBuffer = Cache.circularBuffer;
        var len = circularBuffer.Length;
        var totalReleasedRecords = 0;
        for (var i = 0; i < len; ++i)
        {
            var cacheRecord = circularBuffer[i];
            if (cacheRecord == null || !cacheRecord.IsExpired(ticks)) continue;
            circularBuffer[i] = null;
            ++totalReleasedRecords;
        }
        return totalReleasedRecords;
    }

    public void ClearCache()
    {
        var circularBuffer = Cache.circularBuffer;
        var len = circularBuffer.Length;
        for (var i = 0; i < len; ++i)
        {
            circularBuffer[i] = null;
        }
    }
}