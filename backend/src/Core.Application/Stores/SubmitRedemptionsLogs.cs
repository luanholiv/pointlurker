using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

[ExcludeFromCodeCoverage]
internal static partial class SubmitRedemptionsLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Error,
        Message = "Failed to decrypt the {Provider} credential. Marking its pending redemptions as failed."
    )]
    public static partial void LogSubmitDecryptCredentialFailed(
        this ILogger logger,
        StoreProvider provider,
        Exception ex
    );

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Redemption {RedemptionId} submitted to {Provider} with ID {ProviderRedemptionId} (completed={Completed})."
    )]
    public static partial void LogRedemptionSubmitted(
        this ILogger logger,
        Guid redemptionId,
        StoreProvider provider,
        string? providerRedemptionId,
        bool completed
    );

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} failed with permanent error {StatusCode}: {Reason}."
    )]
    public static partial void LogRedemptionPermanentFailure(
        this ILogger logger,
        Guid redemptionId,
        int statusCode,
        string reason
    );

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} transient failure, scheduled retry at {NextRetryAt}."
    )]
    public static partial void LogRedemptionScheduledRetry(
        this ILogger logger,
        Guid redemptionId,
        DateTimeOffset nextRetryAt,
        Exception ex
    );

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} failed: input coverage lost (missing labels: {MissingLabels})."
    )]
    public static partial void LogRedemptionInputCoverageLost(
        this ILogger logger,
        Guid redemptionId,
        string missingLabels
    );

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Error,
        Message = "Redemption {RedemptionId} failed: input decryption error."
    )]
    public static partial void LogRedemptionInputDecryptionFailed(
        this ILogger logger,
        Guid redemptionId,
        Exception ex
    );

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} refused for insufficient points: {Detail}."
    )]
    public static partial void LogRedemptionInsufficientPoints(
        this ILogger logger,
        Guid redemptionId,
        string detail
    );

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Warning,
        Message = "Redemption {RedemptionId} refused by the provider: {Detail}."
    )]
    public static partial void LogRedemptionRefused(
        this ILogger logger,
        Guid redemptionId,
        string detail
    );

    [LoggerMessage(
        EventId = 2009,
        Level = LogLevel.Critical,
        Message = "Redemption {RedemptionId} was fulfilled with a gift-card code that could not be stored. The redemption is kept as completed and will not be resubmitted; the code is unrecoverable from this application."
    )]
    public static partial void LogFulfillmentCodeNotStored(
        this ILogger logger,
        Guid redemptionId,
        Exception ex
    );
}
