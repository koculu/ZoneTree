
using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.Serializers;

public sealed class ByteArraySerializer : ISerializer<Memory<byte>>
{
    public Memory<byte> Deserialize(Memory<byte> bytes)
    {
        return bytes;
    }

    public Memory<byte> Serialize(in Memory<byte> entry)
    {
        return entry;
    }
}