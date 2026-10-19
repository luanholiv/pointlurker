using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

[ExcludeFromCodeCoverage]
internal static partial class RefreshProviderAccountsLogs
{
    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "Refreshed the {Provider} account name to '{AccountName}'."
    )]
    public static partial void LogAccountRefreshed(
        this ILogger logger,
        StoreProvider provider,
        string accountName
    );

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Warning,
        Message = "Failed to decrypt the {Provider} credential during the account refresh. Retaining the existing account name."
    )]
    public static partial void LogAccountRefreshDecryptFailed(
        this ILogger logger,
        StoreProvider provider,
        Exception ex
    );

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Warning,
        Message = "Failed to refresh the {Provider} account. Retaining the existing account name."
    )]
    public static partial void LogAccountRefreshFailed(
        this ILogger logger,
        StoreProvider provider,
        Exception ex
    );

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "{Provider} refused the stored credential (HTTP {StatusCode}). It needs to be replaced."
    )]
    public static partial void LogAccountRefreshRejected(
        this ILogger logger,
        StoreProvider provider,
        int statusCode
    );

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Warning,
        Message = "Skipping a stored {Provider} credential: no protector is registered for that provider."
    )]
    public static partial void LogAccountRefreshUnsupportedProvider(
        this ILogger logger,
        StoreProvider provider
    );
}
