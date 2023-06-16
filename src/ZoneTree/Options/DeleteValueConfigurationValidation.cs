namespace Tenray.ZoneTree.Options;

/// <summary>
/// Defines the validation behavior 
/// of not providing the delete value delegates.
/// </summary>
public enum DeleteValueConfigurationValidation
{
    /// <summary>
    /// Throws an error if the value deletion delegates are not configured.
    /// </summary>
    Required,

    /// <summary>
    /// Logs a warning if the value deletion delegates are not configured.
    /// </summary>
    Warning,

    /// <summary>
    /// Allows creating ZoneTree without delete record support.
    /// </summary>
    NotRequired,
}
