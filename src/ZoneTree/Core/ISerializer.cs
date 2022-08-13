namespace Tenray.ZoneTree.Core;

public interface ISerializer<TEntry>
{
    TEntry Deserialize(byte[] bytes);

    byte[] Serialize(in TEntry entry);
}
