using Tenray.ZoneTree.PresetTypes;

namespace Tenray.ZoneTree.Serializers;

public sealed class DeletableRefSerializer<TValue> : ISerializer<Deletable<TValue>>
{
    readonly ISerializer<TValue> ValueSerializer;

    public DeletableRefSerializer(ISerializer<TValue> valueSerializer)
    {
        ValueSerializer = valueSerializer;
    }

    public Deletable<TValue> Deserialize(byte[] bytes)
    {
        var isDeletedOffset = bytes.Length - 1;
        var b1 = bytes.AsSpan(0, isDeletedOffset);
        var isDeleted = bytes[isDeletedOffset] != 0;
        return new Deletable<TValue>(
            ValueSerializer.Deserialize(b1.ToArray()),
            isDeleted);
    }

    public byte[] Serialize(in Deletable<TValue> entry)
    {
        var b1 = ValueSerializer.Serialize(entry.Value);
        var len = b1.Length;
        var b2 = new byte[len + 1];
        Array.Copy(b1, b2, len);
        b2[len - 1] = entry.IsDeleted ? (byte)1 : (byte)0;
        return b2;
    }
}