namespace Tenray.ZoneTree.Segments.DiskSegmentVariations;

class CircularCache<TDataType>
{
    class CachedRecord
    {
        public long Index;
        public TDataType Record;
        public long LastAccess;
        public bool IsExpired(int cacheRecordLifeTime) => LastAccess + cacheRecordLifeTime < Environment.TickCount64;
    }

    public CircularCache(int cacheSize, int recordLifeTimeInMillisecond)
    {
        this.CacheSize = cacheSize;
        this.RecordLifeTimeInMillisecond = recordLifeTimeInMillisecond;
        this.CircularCacheRecordBuffer = new CachedRecord[CacheSize];
    }

    CachedRecord[] CircularCacheRecordBuffer;
    int RecordLifeTimeInMillisecond = 10000;
    int statsCacheHit = 0;
    int statsCacheMiss = 0;
    readonly int CacheSize;

    public (int cacheHit, int cacheMiss) GetCacheStats() => (statsCacheHit, statsCacheMiss);

    public bool TryGetFromCache(long index, out TDataType key)
    {
        if (CacheSize < 1)
        {
            key = default;
            return false;
        }
        var circularBuffer = CircularCacheRecordBuffer;
        var len = circularBuffer.Length;
        var circularIndex = index % CacheSize;
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
        if (CacheSize < 1) return false;
        var circularIndex = index % CacheSize;
        var existingCacheRecord = this.CircularCacheRecordBuffer[circularIndex];
        /* Do not add a new cache record when the existing cache record is still active.*/
        if (existingCacheRecord != null &&
            !existingCacheRecord.IsExpired(RecordLifeTimeInMillisecond)) return false;
        var cachedRecord = new CachedRecord
        {
            Index = index,
            Record = key,
            LastAccess = Environment.TickCount64,
        };
        this.CircularCacheRecordBuffer[circularIndex] = cachedRecord;
        return true;
    }


    public int ReleaseInactiveCacheRecords(long ticks)
    {
        var circularBuffer = CircularCacheRecordBuffer;
        var len = circularBuffer.Length;
        var totalReleasedRecords = 0;
        var lifeTime = RecordLifeTimeInMillisecond;
        for (var i = 0; i < len; ++i)
        {
            var cacheRecord = circularBuffer[i];
            if (cacheRecord == null || !cacheRecord.IsExpired(lifeTime)) continue;
            circularBuffer[i] = null;
            ++totalReleasedRecords;
        }
        return totalReleasedRecords;
    }
}