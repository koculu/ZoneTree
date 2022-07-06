namespace Tenray;

public class MissingOptionException : ZoneTreeException
{
    public MissingOptionException(string missingOption)
        : base($"ZoneTree {missingOption} option is not provided.")
    {
        MissingOption = missingOption;
    }

    public string MissingOption { get; }
}
