using System.Runtime.CompilerServices;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.PresetTypes;

/// <summary>
/// Provides utility methods for handling known types, including obtaining comparers, serializers,
/// and determining if values are deleted.
/// </summary>
public static class ComponentsForKnownTypes
{
    /// <summary>
    /// Returns a comparer for a given type key. The comparer is used to compare instances
    /// of the type in ascending order.
    /// </summary>
    /// <typeparam name="TKey">The type of the key to compare.</typeparam>
    /// <returns>An implementation of <see cref="IRefComparer{TKey}"/> appropriate for the type, or null if unsupported.</returns>
    public static IRefComparer<TKey> GetComparer<TKey>()
    {
        TKey key = default;
        var comparer = key switch
        {
            byte => new ByteComparerAscending() as IRefComparer<TKey>,
            char => new CharComparerAscending() as IRefComparer<TKey>,
            DateTime => new DateTimeComparerAscending() as IRefComparer<TKey>,
            decimal => new DecimalComparerAscending() as IRefComparer<TKey>,
            double => new DoubleComparerAscending() as IRefComparer<TKey>,
            short => new Int16ComparerAscending() as IRefComparer<TKey>,
            ushort => new UInt16ComparerAscending() as IRefComparer<TKey>,
            int => new Int32ComparerAscending() as IRefComparer<TKey>,
            uint => new UInt32ComparerAscending() as IRefComparer<TKey>,
            long => new Int64ComparerAscending() as IRefComparer<TKey>,
            ulong => new UInt64ComparerAscending() as IRefComparer<TKey>,
            Guid => new GuidComparerAscending() as IRefComparer<TKey>,
            _ => null
        };
        if (typeof(TKey) == typeof(string))
            comparer =
                new StringOrdinalComparerAscending() as IRefComparer<TKey>;

        else if (typeof(TKey) == typeof(Memory<byte>))
            comparer =
                new ByteArrayComparerAscending() as IRefComparer<TKey>;
        else if (typeof(TKey) == typeof(byte[]))
        {
            throw new ZoneTreeException("ZoneTree<byte[], ...> is not supported. Use ZoneTree<Memory<byte>, ...> instead.");
        }
        return comparer;
    }

    /// <summary>
    /// Returns a serializer for a given type. The serializer is used to serialize and deserialize
    /// instances of the type.
    /// </summary>
    /// <typeparam name="T">The type to be serialized.</typeparam>
    /// <returns>An implementation of <see cref="ISerializer{T}"/> appropriate for the type, or null if unsupported.</returns>
    public static ISerializer<T> GetSerializer<T>()
    {
        T key = default;
        var serializer = key switch
        {
            byte => new ByteSerializer() as ISerializer<T>,
            bool => new BooleanSerializer() as ISerializer<T>,
            char => new CharSerializer() as ISerializer<T>,
            DateTime => new DateTimeSerializer() as ISerializer<T>,
            decimal => new DecimalSerializer() as ISerializer<T>,
            double => new DoubleSerializer() as ISerializer<T>,
            short => new Int16Serializer() as ISerializer<T>,
            ushort => new UInt16Serializer() as ISerializer<T>,
            int => new Int32Serializer() as ISerializer<T>,
            uint => new UInt32Serializer() as ISerializer<T>,
            long => new Int64Serializer() as ISerializer<T>,
            ulong => new UInt64Serializer() as ISerializer<T>,
            Guid => new StructSerializer<Guid>() as ISerializer<T>,
            _ => null
        };

        if (typeof(T) == typeof(string))
            serializer =
                new Utf8StringSerializer() as ISerializer<T>;
        else if (typeof(T) == typeof(Memory<byte>))
        {
            serializer =
                new ByteArraySerializer() as ISerializer<T>;
        }
        else if (typeof(T) == typeof(byte[]))
        {
            throw new ZoneTreeException("ZoneTree<byte[], ...> is not supported. Use ZoneTree<Memory<byte>, ...> instead.");
        }
        return serializer;
    }

    // Specific methods for checking if certain primitive types are considered deleted
    static bool IsValueDeletedByte<TKey>(in TKey key, in byte value) => value == default;
    static bool IsValueDeletedChar<TKey>(in TKey key, in char value) => value == default;
    static bool IsValueDeletedDateTime<TKey>(in TKey key, in DateTime value) => value == default;
    static bool IsValueDeletedDecimal<TKey>(in TKey key, in decimal value) => value == default;
    static bool IsValueDeletedDouble<TKey>(in TKey key, in double value) => value == default;
    static bool IsValueDeletedShort<TKey>(in TKey key, in short value) => value == default;
    static bool IsValueDeletedUShort<TKey>(in TKey key, in ushort value) => value == default;
    static bool IsValueDeletedInt<TKey>(in TKey key, in int value) => value == default;
    static bool IsValueDeletedUInt<TKey>(in TKey key, in uint value) => value == default;
    static bool IsValueDeletedLong<TKey>(in TKey key, in long value) => value == default;
    static bool IsValueDeletedULong<TKey>(in TKey key, in ulong value) => value == default;
    static bool IsValueDeletedGuid<TKey>(in TKey key, in Guid value) => value == default;
    static bool IsValueDeletedMemoryByte<TKey>(in TKey key, in Memory<byte> value) => value.Length == 0;
    static bool IsValueDeletedReferenceType<TKey, TValue>(in TKey key, in TValue value) => ReferenceEquals(value, default(TValue));
    static bool IsValueDeletedDefault<TKey, TValue>(in TKey key, in TValue value) => EqualityComparer<TValue>.Default.Equals(value, default);

    static void MarkValueDeletedDefault<TValue>(ref TValue value) { value = default; }

    /// <summary>
    /// Returns a delegate that checks if a key-value pair of a specific type is considered deleted.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>A delegate that checks if the value is considered deleted.</returns>
    public static IsValueDeletedDelegate<TKey, TValue> GetIsValueDeleted<TKey, TValue>()
    {
        static IsValueDeletedDelegate<TKey, TValue> Cast(object method) =>
            (IsValueDeletedDelegate<TKey, TValue>)method;
        TValue value = default;
        var result = value switch
        {
            byte => Cast(new IsValueDeletedDelegate<TKey, byte>(IsValueDeletedByte)),
            char => Cast(new IsValueDeletedDelegate<TKey, char>(IsValueDeletedChar)),
            DateTime => Cast(new IsValueDeletedDelegate<TKey, DateTime>(IsValueDeletedDateTime)),
            decimal => Cast(new IsValueDeletedDelegate<TKey, decimal>(IsValueDeletedDecimal)),
            double => Cast(new IsValueDeletedDelegate<TKey, double>(IsValueDeletedDouble)),
            short => Cast(new IsValueDeletedDelegate<TKey, short>(IsValueDeletedShort)),
            ushort => Cast(new IsValueDeletedDelegate<TKey, ushort>(IsValueDeletedUShort)),
            int => Cast(new IsValueDeletedDelegate<TKey, int>(IsValueDeletedInt)),
            uint => Cast(new IsValueDeletedDelegate<TKey, uint>(IsValueDeletedUInt)),
            long => Cast(new IsValueDeletedDelegate<TKey, long>(IsValueDeletedLong)),
            ulong => Cast(new IsValueDeletedDelegate<TKey, ulong>(IsValueDeletedULong)),
            Guid => Cast(new IsValueDeletedDelegate<TKey, Guid>(IsValueDeletedGuid)),
            _ => IsValueDeletedDefault
        };

        if (typeof(TValue) == typeof(Memory<byte>))
            result = Cast(new IsValueDeletedDelegate<TKey, Memory<byte>>(IsValueDeletedMemoryByte));
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            return IsValueDeletedReferenceType;
        return result;
    }

    /// <summary>
    /// Returns a delegate that marks a value of a specific type as deleted by setting it to its default value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>A delegate that marks the value as deleted.</returns>
    public static MarkValueDeletedDelegate<TValue> GetMarkValueDeleted<TValue>()
    {
        return MarkValueDeletedDefault;
    }
}