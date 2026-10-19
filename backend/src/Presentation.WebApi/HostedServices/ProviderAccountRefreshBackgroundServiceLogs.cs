using System.Diagnostics.CodeAnalysis;

namespace PointLurker.Presentation.WebApi.HostedServices;

[ExcludeFromCodeCoverage]
internal static partial class ProviderAccountRefreshBackgroundServiceLogs
{
    [LoggerMessage(
        EventId = 3110,
        Level = LogLevel.Information,
        Message = "Account refresh scheduled with cron '{CronExpression}'."
    )]
    public static partial void LogUsernameRefreshScheduled(
        this ILogger logger,
        string cronExpression
    );

    [LoggerMessage(
        EventId = 3111,
        Level = LogLevel.Error,
        Message = "Account refresh cron has no next occurrence; refresh loop stopping."
    )]
    public static partial void LogUsernameRefreshNoNextOccurrence(this ILogger logger);

    [LoggerMessage(
        EventId = 3112,
        Level = LogLevel.Error,
        Message = "Account refresh cycle failed unexpectedly."
    )]
    public static partial void LogUsernameRefreshCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 3113,
        Level = LogLevel.Debug,
        Message = "Account refresh cancelled during shutdown."
    )]
    public static partial void LogUsernameRefreshCancelled(this ILogger logger);

    [LoggerMessage(
        EventId = 3114,
        Level = LogLevel.Error,
        Message = "Credential expiry warning pass failed unexpectedly."
    )]
    public static partial void LogCredentialExpiryWarningPassFailed(
        this ILogger logger,
        Exception ex
    );

    [LoggerMessage(
        EventId = 3115,
        Level = LogLevel.Debug,
        Message = "Credential expiry warning pass cancelled during shutdown. Any warning still owed goes out on the next pass."
    )]
    public static partial void LogCredentialExpiryWarningCancelled(this ILogger logger);
}
