namespace Tenray.ZoneTree.Segments.Model;

public struct EntryBody : IEquatable<EntryBody>
{
    public byte[] Key;

    public byte[] Value;

    public override bool Equals(object obj)
    {
        return obj is EntryBody body && Equals(body);
    }

    public bool Equals(EntryBody other)
    {
        return EqualityComparer<byte[]>.Default.Equals(Key, other.Key) &&
               EqualityComparer<byte[]>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value);
    }

    public static bool operator ==(EntryBody left, EntryBody right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EntryBody left, EntryBody right)
    {
        return !(left == right);
    }
}
