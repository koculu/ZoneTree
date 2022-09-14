namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeKeyTypeMismatchException : ZoneTreeException
{
    public TreeKeyTypeMismatchException(string expectedKeyType, string givenKeyType)
        : base($"Tree key type does not match.\r\n expected: {expectedKeyType}\r\n given: {givenKeyType}")
    {
        ExpectedKeyType = expectedKeyType;
        GivenKeyType = givenKeyType;
    }

    public string ExpectedKeyType { get; }
    public string GivenKeyType { get; }
}
