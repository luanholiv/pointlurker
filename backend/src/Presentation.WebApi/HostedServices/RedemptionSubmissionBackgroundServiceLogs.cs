using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Presentation.WebApi.HostedServices;

[ExcludeFromCodeCoverage]
internal static partial class RedemptionSubmissionBackgroundServiceLogs
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Error,
        Message = "Redemption submission cycle failed."
    )]
    public static partial void LogSubmissionCycleFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Redemption submission sweeping every {SweepInterval} in addition to waking on newly created redemptions."
    )]
    public static partial void LogSubmissionSweepScheduled(
        this ILogger logger,
        TimeSpan sweepInterval
    );

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Error,
        Message = "The redemption wake-up channel is no longer usable. Falling back to the sweep interval alone; submission continues, but a newly created redemption now waits for the next sweep."
    )]
    public static partial void LogSubmissionWakeUpUnavailable(this ILogger logger, Exception ex);
}
