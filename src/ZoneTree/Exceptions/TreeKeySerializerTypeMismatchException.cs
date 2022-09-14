namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeKeySerializerTypeMismatchException : ZoneTreeException
{
    public TreeKeySerializerTypeMismatchException(string expectedType, string givenType)
        : base($"Tree key serializer type does not match.\r\n expected: {expectedType}\r\n given: {givenType}")
    {
        ExpectedType = expectedType;
        GivenType = givenType;
    }

    public string ExpectedType { get; }
    public string GivenType { get; }
}
