using Microsoft.Extensions.Logging;

namespace Tenray.ZoneTree.Extensions;

public static class LoggerExtensions
{
    /// <summary>
    /// Formats and writes a error log message.
    /// </summary>
    /// <param name="logger">logger</param>
    /// <param name="exception">The exception to log.</param>
    public static void LogException(this ILogger logger, Exception exception)
    {
        logger.LogError(exception, exception?.Message);
    }
    
    /// <summary>
    /// Formats and writes a error log message.
    /// </summary>
    /// <param name="logger">logger</param>
    /// <param name="exception">The exception to log.</param>
    public static void LogCriticalException(this ILogger logger, Exception exception)
    {
        logger.LogCritical(exception, exception?.Message);
    }
    
    /// <summary>
    /// Formats and writes a error log message.
    /// </summary>
    /// <param name="logger">logger</param>
    /// <param name="exception">The exception to log.</param>
    public static void LogWarningException(this ILogger logger, Exception exception)
    {
        logger.LogWarning(exception, exception?.Message);
    }
}