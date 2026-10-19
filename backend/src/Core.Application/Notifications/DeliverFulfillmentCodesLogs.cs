using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Core.Application.Notifications;

[ExcludeFromCodeCoverage]
internal static partial class DeliverFulfillmentCodesLogs
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "Gift-card code for redemption {RedemptionId} emailed to the owner."
    )]
    public static partial void LogFulfillmentCodeDelivered(this ILogger logger, Guid redemptionId);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Gift-card code for redemption {RedemptionId} not delivered (attempt {Attempts}); retrying at {NextRetryAt}: {Failure}."
    )]
    public static partial void LogFulfillmentCodeDeliveryFailed(
        this ILogger logger,
        Guid redemptionId,
        int attempts,
        DateTimeOffset nextRetryAt,
        string failure
    );

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Gift-card code for redemption {RedemptionId} could not be decrypted. Check that the Data Protection key ring is the one the code was stored under."
    )]
    public static partial void LogFulfillmentCodeDecryptionFailed(
        this ILogger logger,
        Guid redemptionId,
        Exception ex
    );
}
