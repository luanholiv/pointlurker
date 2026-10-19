namespace PointLurker.Presentation.WebApi.Cors;

/// <summary>
/// Browser origins allowed to call the API cross-origin, bound to the <c>Cors</c> configuration
/// section. Deliberately NOT named <c>CorsOptions</c>, which collides with ASP.NET Core's own
/// <c>Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions</c>.
/// </summary>
public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Absolute <c>http</c>/<c>https</c> origins (scheme + host + optional port, no trailing slash)
    /// allowed to call the API from a browser. Empty — the default — means no CORS policy is applied
    /// at all, except in Development, which falls back to a permissive policy for the Vite dev
    /// server. In the shipped topology the frontend nginx proxies <c>/api</c>, so requests are
    /// same-origin and this stays empty.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
