using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace PointLurker.Presentation.WebApi;

[ExcludeFromCodeCoverage]
internal static partial class ProgramLogs
{
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Critical,
        Message = "Configuration validation failure: {Failure}"
    )]
    public static partial void LogConfigurationValidationFailure(
        this ILogger logger,
        string failure
    );
}
