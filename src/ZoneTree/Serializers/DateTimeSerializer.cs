namespace Tenray.ZoneTree.Serializers;

public sealed class DateTimeSerializer : ISerializer<DateTime>
{
    public DateTime Deserialize(Memory<byte> bytes)
    {
        return new DateTime(BitConverter.ToInt64(bytes.Span));
    }

    public Memory<byte> Serialize(in DateTime entry)
    {
        return BitConverter.GetBytes(entry.Ticks);
    }
}
