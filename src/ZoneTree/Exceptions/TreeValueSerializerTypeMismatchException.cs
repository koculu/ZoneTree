namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeValueSerializerTypeMismatchException : ZoneTreeException
{
    public TreeValueSerializerTypeMismatchException(string expectedType, string givenType)
        : base($"Tree value serializer type does not match.\r\n expected: {expectedType}\r\n given: {givenType}")
    {
        ExpectedType = expectedType;
        GivenType = givenType;
    }

    public string ExpectedType { get; }
    public string GivenType { get; }
}