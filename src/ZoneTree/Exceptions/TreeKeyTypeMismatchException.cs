namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeKeyTypeMismatchException : ZoneTreeException
{
    public TreeKeyTypeMismatchException(string expectedKeyType, string givenKeyType)
        : base($"Tree key type does not match.\r\nValue in metadata (JSON): {expectedKeyType}\r\nValue in Runtime: {givenKeyType}\r\n" +
               "This could be due to a class rename. If the type mismatch is intentional (e.g., after a refactor), " +
               "you may fix this error by manually editing the metadata JSON file.")
    {
        ExpectedKeyType = expectedKeyType;
        GivenKeyType = givenKeyType;
    }

    public string ExpectedKeyType { get; }
    public string GivenKeyType { get; }
}
