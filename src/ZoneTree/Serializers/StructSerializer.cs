
using System;

namespace Tenray.ZoneTree.Serializers;

public sealed class StructSerializer<TType> : ISerializer<TType> where TType : unmanaged
{
    public TType Deserialize(Memory<byte> bytes)
    {
        return BinarySerializerHelper.FromByteArray<TType>(bytes);
    }

    public Memory<byte> Serialize(in TType entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}