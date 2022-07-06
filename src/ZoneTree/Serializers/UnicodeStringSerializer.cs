using System.Text;
using ZoneTree.Core;

namespace ZoneTree.Serializers
{
    public class UnicodeStringSerializer : ISerializer<string>
    {
        public string Deserialize(byte[] bytes)
        {
            return Encoding.Unicode.GetString(bytes);
        }

        public byte[] Serialize(string entry)
        {
            return Encoding.Unicode.GetBytes(entry);
        }
    }
}
