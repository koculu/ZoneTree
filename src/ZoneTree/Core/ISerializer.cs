namespace ZoneTree.Core;

public interface ISerializer<TEntry>
{
    TEntry Deserialize(byte[] bytes);

    byte[] Serialize(TEntry entry);
}
