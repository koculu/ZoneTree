namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeValueSerializerTypeMismatchException : ZoneTreeException
{
    public TreeValueSerializerTypeMismatchException(string expectedType, string givenType)
        : base($"Tree value serializer type does not match.\r\nValue in metadata (JSON): {expectedType}\r\nValue in Runtime: {givenType}\r\n" +
               "This could be due to a class rename. If the type mismatch is intentional (e.g., after a refactor), " +
               "you may fix this error by manually editing the metadata JSON file.")
    {
        ExpectedType = expectedType;
        GivenType = givenType;
    }

    public string ExpectedType { get; }
    public string GivenType { get; }
}
