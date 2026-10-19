using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

[ExcludeFromCodeCoverage]
internal static partial class WarnExpiringCredentialsLogs
{
    [LoggerMessage(
        EventId = 3130,
        Level = LogLevel.Information,
        Message = "Warned the owner that the {Provider} credential expires at {ExpiresAt:o}."
    )]
    public static partial void LogCredentialExpiryWarningSent(
        this ILogger logger,
        StoreProvider provider,
        DateTimeOffset expiresAt
    );

    [LoggerMessage(
        EventId = 3131,
        Level = LogLevel.Warning,
        Message = "Failed to send the expiry warning for the {Provider} credential expiring at {ExpiresAt:o}. The next pass tries again."
    )]
    public static partial void LogCredentialExpiryWarningFailed(
        this ILogger logger,
        StoreProvider provider,
        DateTimeOffset expiresAt,
        Exception ex
    );

    [LoggerMessage(
        EventId = 3132,
        Level = LogLevel.Warning,
        Message = "{Count} credential expiry warning(s) are owed, but the account has no email address to send them to."
    )]
    public static partial void LogCredentialExpiryWarningNoAddress(this ILogger logger, int count);
}
