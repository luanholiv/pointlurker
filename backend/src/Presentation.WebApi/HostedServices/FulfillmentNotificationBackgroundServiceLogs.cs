using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Presentation.WebApi.HostedServices;

[ExcludeFromCodeCoverage]
internal static partial class FulfillmentNotificationBackgroundServiceLogs
{
    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Error,
        Message = "Gift-card delivery cycle failed."
    )]
    public static partial void LogFulfillmentCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Information,
        Message = "Gift-card delivery sweeping every {SweepInterval} in addition to waking on newly stored codes."
    )]
    public static partial void LogFulfillmentSweepScheduled(
        this ILogger logger,
        TimeSpan sweepInterval
    );

    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Error,
        Message = "The gift-card wake-up channel is no longer usable. Falling back to the sweep interval alone; delivery continues, but a newly stored code now waits for the next sweep."
    )]
    public static partial void LogFulfillmentWakeUpUnavailable(this ILogger logger, Exception ex);
}
