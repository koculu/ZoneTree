namespace ZoneTree.Exceptions;

public sealed class InvalidOptionValueException : ZoneTreeException
{
  public InvalidOptionValueException(
      string option,
      object value,
      string rule)
      : base($"ZoneTree {option} option value ({value}) is not valid. {rule}.")
  {
    Option = option;
    Value = value;
    Rule = rule;
  }

  public string Option { get; }

  public object Value { get; }

  public string Rule { get; }
}
