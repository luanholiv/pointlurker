using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Presentation.WebApi.HostedServices;

[ExcludeFromCodeCoverage]
internal static partial class StoreSyncBackgroundServiceLogs
{
    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Store sync scheduled with cron expression: {CronExpression}"
    )]
    public static partial void LogSyncScheduled(this ILogger logger, string cronExpression);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Warning,
        Message = "No next occurrence found for cron expression. Store sync stopping."
    )]
    public static partial void LogNoNextOccurrence(this ILogger logger);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Previous sync cycle still running. Skipping this tick."
    )]
    public static partial void LogSyncAlreadyRunning(this ILogger logger);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Information,
        Message = "Starting store sync cycle."
    )]
    public static partial void LogSyncCycleStarting(this ILogger logger);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Information,
        Message = "Store sync cycle completed. Stores: {StoresSynced} synced, {StoresSkipped} not yet due, Items: {ItemsChanged} changed of {ItemsProcessed}, Redemptions: {Attempted}/{Succeeded}, Notifications: {Sent}"
    )]
    public static partial void LogSyncCycleCompleted(
        this ILogger logger,
        int storesSynced,
        int storesSkipped,
        int itemsChanged,
        int itemsProcessed,
        int attempted,
        int succeeded,
        int sent
    );

    [LoggerMessage(EventId = 3008, Level = LogLevel.Error, Message = "Store sync cycle failed.")]
    public static partial void LogSyncCycleFailed(this ILogger logger, Exception ex);
}
