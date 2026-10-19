using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Presentation.WebApi.HostedServices;

[ExcludeFromCodeCoverage]
internal static partial class RedemptionPollingBackgroundServiceLogs
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Redemption polling scheduled with cron: {CronExpression}."
    )]
    public static partial void LogPollingScheduled(this ILogger logger, string cronExpression);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Error,
        Message = "Redemption polling cycle failed."
    )]
    public static partial void LogPollingCycleFailed(this ILogger logger, Exception ex);
}
