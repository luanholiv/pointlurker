using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Infrastructure.Database;

[ExcludeFromCodeCoverage]
internal static partial class DatabaseInitializerLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Applying database migrations..."
    )]
    public static partial void LogApplyingMigrations(this ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Database migrations completed successfully."
    )]
    public static partial void LogMigrationsCompleted(this ILogger logger);

    // EventIds 2003/2004 retired with the one-shot startup backfills (LogEncryptingToken,
    // LogBackfilledRedemptionMessages) — numbering intentionally left with a gap.
}
