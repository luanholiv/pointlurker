namespace PointLurker.Presentation.WebApi.Cors;

/// <summary>How the CORS middleware should be configured for this run.</summary>
internal enum CorsMode
{
    /// <summary>No policy registered and no CORS middleware — no response ever carries CORS headers.</summary>
    Disabled,

    /// <summary>Any origin allowed. Development-only fallback for the cross-origin Vite dev server.</summary>
    Permissive,

    /// <summary>Only the configured origins allowed.</summary>
    Restricted,
}

/// <summary>
/// Single source of truth for the CORS decision, shared by <c>AddConfiguredCors</c> and
/// <c>UseConfiguredCors</c> so the registration and the pipeline can never disagree.
/// </summary>
/// <remarks>
/// Reads raw configuration rather than <c>IOptions&lt;CorsSettings&gt;</c> on purpose: resolving the
/// options while the pipeline is being built would run validation before <c>RunAsync</c>, so an
/// <c>OptionsValidationException</c> would escape the handler in <c>Program.cs</c> that logs each
/// configuration failure. <c>ValidateOnStart</c> still fails fast inside <c>RunAsync</c>, where it is
/// caught and logged.
/// </remarks>
internal static class CorsPolicyResolver
{
    internal static (CorsMode Mode, string[] Origins) Resolve(
        IConfiguration configuration,
        bool isDevelopment
    )
    {
        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();
        var origins = Normalize(settings?.AllowedOrigins);

        if (origins.Length > 0)
            return (CorsMode.Restricted, origins);

        return isDevelopment ? (CorsMode.Permissive, origins) : (CorsMode.Disabled, origins);
    }

    /// <summary>
    /// Trims, drops blank entries, lowercases and dedupes. Blank entries matter: an unset
    /// <c>CORS__ALLOWEDORIGINS__0</c> passthrough in Docker Compose binds to a single empty-string
    /// element, which would otherwise flip the mode to <see cref="CorsMode.Restricted"/> with a
    /// garbage origin. Lowercasing matters because ASP.NET Core compares the browser's
    /// <c>Origin</c> header ordinally and browsers send scheme and host in lower case; an origin has
    /// no path component, so lowercasing the whole value is safe.
    /// </summary>
    private static string[] Normalize(string[]? origins) =>
        origins is null
            ? []
            :
            [
                .. origins
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Select(origin => origin.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal),
            ];
}
