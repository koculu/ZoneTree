using ZoneTree.Core;

namespace ZoneTree.Serializers
{
    public class BooleanSerializer : ISerializer<bool>
    {
        public bool Deserialize(byte[] bytes)
        {
            return BitConverter.ToBoolean(bytes);
        }

        public byte[] Serialize(bool entry)
        {
            return BitConverter.GetBytes(entry);
        }
    }
}
