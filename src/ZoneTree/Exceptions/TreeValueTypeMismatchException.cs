namespace Tenray.ZoneTree.Exceptions;

public sealed class TreeValueTypeMismatchException : ZoneTreeException
{
    public TreeValueTypeMismatchException(string expectedValueType, string givenValueType)
        : base($"Tree value type does not match.\r\n expected: {expectedValueType}\r\n given: {givenValueType}")
    {
        ExpectedValueType = expectedValueType;
        GivenValueType = givenValueType;
    }

    public string ExpectedValueType { get; }
    public string GivenValueType { get; }
}
