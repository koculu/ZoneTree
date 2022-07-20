namespace Tenray.ZoneTree.WAL;

public struct KeyValuePocket<TKey, TValue>
{
    public TKey Key;
    public TValue Value;

    public KeyValuePocket(TKey key, TValue value) : this()
    {
        Key = key;
        Value = value;
    }
}

