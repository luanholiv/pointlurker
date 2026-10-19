using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

[ExcludeFromCodeCoverage]
internal static partial class PollRedemptionStatusLogs
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Failed to decrypt the {Provider} credential during polling. Its completion checks are skipped this cycle; outstanding redemptions can still time out."
    )]
    public static partial void LogPollDecryptCredentialFailed(
        this ILogger logger,
        StoreProvider provider,
        Exception ex
    );

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} was rejected by the provider at {RejectedAt}."
    )]
    public static partial void LogRedemptionRejected(
        this ILogger logger,
        Guid redemptionId,
        DateTimeOffset rejectedAt
    );

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Error,
        Message = "Completion status for redemption {RedemptionId} has now failed to read {PollFailures} times in a row. The redemption itself was submitted; it is the status check that is broken."
    )]
    public static partial void LogPollRedemptionPersistentlyFailing(
        this ILogger logger,
        Guid redemptionId,
        int pollFailures
    );

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} timed out (submitted at {SubmittedAt}). Marking as CompletionTimedOut."
    )]
    public static partial void LogRedemptionTimedOut(
        this ILogger logger,
        Guid redemptionId,
        DateTimeOffset submittedAt
    );

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Redemption {RedemptionId} not yet completed by the provider."
    )]
    public static partial void LogRedemptionNotYetCompleted(this ILogger logger, Guid redemptionId);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Redemption {RedemptionId} completed by the provider at {CompletedAt}."
    )]
    public static partial void LogRedemptionCompleted(
        this ILogger logger,
        Guid redemptionId,
        DateTimeOffset completedAt
    );

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Failed to poll provider completion status for redemption {RedemptionId} ({PollFailures} consecutive failures). Status left as Submitted."
    )]
    public static partial void LogPollRedemptionFailed(
        this ILogger logger,
        Guid redemptionId,
        int pollFailures,
        Exception ex
    );
}
