
using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.Serializers;

public sealed class ByteArraySerializer : ISerializer<Memory<byte>>
{
    public Memory<byte> Deserialize(Memory<byte> bytes)
    {
        // Need to create new byte array.
        // Otherwise, the data in memory would attach to the block caches.
        return bytes.ToArray();
    }

    public Memory<byte> Serialize(in Memory<byte> entry)
    {
        return entry;
    }
}