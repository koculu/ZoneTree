namespace Tenray.ZoneTree.Collections;

public interface IIndexedReader<TKey, TValue>
{
    long Length { get; }

    TKey GetKey(long index);

    TValue GetValue(long index);

    long GetLastSmallerOrEqualPosition(in TKey key);

    long GetFirstGreaterOrEqualPosition(in TKey key);

    bool IsBeginningOfAPart(long index);

    bool IsEndOfAPart(long index);
    
    int GetPartIndex(long index);
}


