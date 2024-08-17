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
    static bool IsValueDeletedByte(in byte value) => value == default;
    static bool IsValueDeletedChar(in char value) => value == default;
    static bool IsValueDeletedDateTime(in DateTime value) => value == default;
    static bool IsValueDeletedDecimal(in decimal value) => value == default;
    static bool IsValueDeletedDouble(in double value) => value == default;
    static bool IsValueDeletedShort(in short value) => value == default;
    static bool IsValueDeletedUShort(in ushort value) => value == default;
    static bool IsValueDeletedInt(in int value) => value == default;
    static bool IsValueDeletedUInt(in uint value) => value == default;
    static bool IsValueDeletedLong(in long value) => value == default;
    static bool IsValueDeletedULong(in ulong value) => value == default;
    static bool IsValueDeletedGuid(in Guid value) => value == default;
    static bool IsValueDeletedMemoryByte(in Memory<byte> value) => value.Length == 0;
    static bool IsValueDeletedReferenceType<TValue>(in TValue value) => ReferenceEquals(value, default(TValue));
    static bool IsValueDeletedDefault<TValue>(in TValue value) => EqualityComparer<TValue>.Default.Equals(value, default);

    static void MarkValueDeletedDefault<TValue>(ref TValue value) { value = default; }

    /// <summary>
    /// Returns a delegate that checks if a value of a specific type is considered deleted.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <returns>A delegate that checks if the value is considered deleted.</returns>
    public static IsValueDeletedDelegate<TValue> GetIsValueDeleted<TValue>()
    {
        static IsValueDeletedDelegate<TValue> Cast(object method) =>
            (IsValueDeletedDelegate<TValue>)method;
        TValue value = default;
        var result = value switch
        {
            byte => Cast(new IsValueDeletedDelegate<byte>(IsValueDeletedByte)),
            char => Cast(new IsValueDeletedDelegate<char>(IsValueDeletedChar)),
            DateTime => Cast(new IsValueDeletedDelegate<DateTime>(IsValueDeletedDateTime)),
            decimal => Cast(new IsValueDeletedDelegate<decimal>(IsValueDeletedDecimal)),
            double => Cast(new IsValueDeletedDelegate<double>(IsValueDeletedDouble)),
            short => Cast(new IsValueDeletedDelegate<short>(IsValueDeletedShort)),
            ushort => Cast(new IsValueDeletedDelegate<ushort>(IsValueDeletedUShort)),
            int => Cast(new IsValueDeletedDelegate<int>(IsValueDeletedInt)),
            uint => Cast(new IsValueDeletedDelegate<uint>(IsValueDeletedUInt)),
            long => Cast(new IsValueDeletedDelegate<long>(IsValueDeletedLong)),
            ulong => Cast(new IsValueDeletedDelegate<ulong>(IsValueDeletedULong)),
            Guid => Cast(new IsValueDeletedDelegate<Guid>(IsValueDeletedGuid)),
            _ => IsValueDeletedDefault
        };

        if (typeof(TValue) == typeof(Memory<byte>))
            result = Cast(new IsValueDeletedDelegate<Memory<byte>>(IsValueDeletedMemoryByte));
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