namespace Tenray.ZoneTree.Collections;

public interface IIndexedReader<TKey, TValue>
{
    int Length { get; }

    TKey GetKey(int index);

    TValue GetValue(int index);

    int GetLastSmallerOrEqualPosition(in TKey key);

    int GetFirstGreaterOrEqualPosition(in TKey key);

    bool IsBeginningOfASector(int index);

    bool IsEndOfASector(int index);
    
    int GetSectorIndex(int index);
}


