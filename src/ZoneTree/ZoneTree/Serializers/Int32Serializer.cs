using ZoneTree.Core;

namespace ZoneTree.Serializers
{
    public class Int32Serializer : ISerializer<int>
    {
        public int Deserialize(byte[] bytes)
        {
            return BitConverter.ToInt32(bytes);
        }

        public byte[] Serialize(int entry)
        {
            return BitConverter.GetBytes(entry);
        }
    }
}
