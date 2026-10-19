namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Configurable lifetimes for the Identity Bearer scheme's app-session tokens, bound to the
/// <c>Authentication</c> configuration section. Deliberately NOT named <c>AuthenticationOptions</c>,
/// which collides with ASP.NET Core's own <c>AuthenticationOptions</c>.
/// </summary>
public sealed class AuthTokenOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Access-token lifetime in minutes. Default 30, minimum 1.</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 30;

    /// <summary>Refresh-token lifetime in hours. Default 24, minimum 1.</summary>
    public int RefreshTokenExpirationHours { get; set; } = 24;
}
