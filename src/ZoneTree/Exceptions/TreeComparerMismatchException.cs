namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeComparerMismatchException : ZoneTreeException
{
    public TreeComparerMismatchException(string expectedComparerType, string givenComparerType)
        : base($"Tree comparer does not match.\r\n expected: {expectedComparerType}\r\n given: {givenComparerType}")
    {
        ExpectedComparerType = expectedComparerType;
        GivenComparerType = givenComparerType;
    }

    public string ExpectedComparerType { get; }
    public string GivenComparerType { get; }
}
