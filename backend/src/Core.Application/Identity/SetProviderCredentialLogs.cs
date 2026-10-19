using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

[ExcludeFromCodeCoverage]
internal static partial class SetProviderCredentialLogs
{
    [LoggerMessage(
        EventId = 3120,
        Level = LogLevel.Information,
        Message = "{Provider} refused the credential being saved (HTTP {StatusCode}). Nothing was stored."
    )]
    public static partial void LogCredentialRejected(
        this ILogger logger,
        StoreProvider provider,
        int statusCode
    );

    [LoggerMessage(
        EventId = 3121,
        Level = LogLevel.Warning,
        Message = "Could not confirm the {Provider} credential being saved (HTTP {StatusCode}); storing it unconfirmed."
    )]
    public static partial void LogCredentialUnconfirmed(
        this ILogger logger,
        StoreProvider provider,
        int statusCode
    );
}
