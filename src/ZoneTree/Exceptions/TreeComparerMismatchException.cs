namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeComparerMismatchException : ZoneTreeException
{
    public TreeComparerMismatchException(string expectedComparerType, string givenComparerType)
        : base($"Tree comparer does not match.\r\nValue in metadata (JSON): {expectedComparerType}\r\nValue in Runtime: {givenComparerType}\r\n" +
               "This could be due to a class rename. If the type mismatch is intentional (e.g., after a refactor), " +
               "you may fix this error by manually editing the metadata JSON file.")
    {
        ExpectedComparerType = expectedComparerType;
        GivenComparerType = givenComparerType;
    }

    public string ExpectedComparerType { get; }
    public string GivenComparerType { get; }
}
