namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeValueTypeMismatchException : ZoneTreeException
{
    public TreeValueTypeMismatchException(string expectedValueType, string givenValueType)
        : base($"Tree value type does not match.\r\nValue in metadata (JSON): {expectedValueType}\r\nValue in Runtime: {givenValueType}\r\n" +
               "This could be due to a class rename. If the type mismatch is intentional (e.g., after a refactor), " +
               "you may fix this error by manually editing the metadata JSON file.")
    {
        ExpectedValueType = expectedValueType;
        GivenValueType = givenValueType;
    }

    public string ExpectedValueType { get; }
    public string GivenValueType { get; }
}
