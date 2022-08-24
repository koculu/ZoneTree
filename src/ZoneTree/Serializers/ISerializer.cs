namespace Tenray.ZoneTree.Serializers;

/// <summary>
/// Generic Serializer interface for any type.
/// </summary>
/// <typeparam name="TEntry"></typeparam>
public interface ISerializer<TEntry>
{
    /// <summary>
    /// Deserialize the bytes into entry type.
    /// </summary>
    /// <param name="bytes">The bytes to be deserialized.</param>
    /// <returns>The deserialized entry.</returns>
    TEntry Deserialize(byte[] bytes);

    /// <summary>
    /// Serialize the entry into byte array.
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <returns>The serialized bytes.</returns>
    byte[] Serialize(in TEntry entry);
}
