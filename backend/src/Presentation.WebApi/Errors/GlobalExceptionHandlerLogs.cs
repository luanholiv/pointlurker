using System.Diagnostics.CodeAnalysis;

namespace PointLurker.Presentation.WebApi.Errors;

[ExcludeFromCodeCoverage]
internal static partial class GlobalExceptionHandlerLogs
{
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Error,
        Message = "Unhandled exception on {Method} {Path}"
    )]
    public static partial void LogUnhandledException(
        this ILogger logger,
        string method,
        string path,
        Exception ex
    );

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Warning,
        Message = "Unreadable request on {Method} {Path}; responding {StatusCode}"
    )]
    public static partial void LogUnreadableRequest(
        this ILogger logger,
        string method,
        string path,
        int statusCode,
        Exception ex
    );
}
