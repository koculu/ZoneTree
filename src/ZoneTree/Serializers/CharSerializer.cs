namespace Tenray.ZoneTree.Serializers;

public sealed class CharSerializer : ISerializer<char>
{
    public char Deserialize(byte[] bytes)
    {
        return (char)bytes[0];
    }

    public byte[] Serialize(in char entry)
    {
        return new byte[1] { (byte)entry };
    }
}