namespace Tenray.ZoneTree.Serializers;

public sealed class BooleanSerializer : ISerializer<bool>
{
    public bool Deserialize(byte[] bytes)
    {
        return BitConverter.ToBoolean(bytes);
    }

    public byte[] Serialize(in bool entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
