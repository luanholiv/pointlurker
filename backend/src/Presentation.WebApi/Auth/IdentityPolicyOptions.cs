namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Configurable account policy applied to ASP.NET Identity — lockout and password length. Bound to
/// the same <c>Authentication</c> section as <see cref="AuthTokenOptions"/> on purpose: both are
/// auth knobs a self-hoster tunes together, so they share one section (and one
/// <c>docs/CONFIGURATION.md</c> table) rather than splitting across two. Deliberately NOT named
/// <c>IdentityOptions</c>, which collides with ASP.NET Core's own <c>IdentityOptions</c> — the type
/// these values are applied to.
/// </summary>
/// <remarks>
/// Only lockout and password length are exposed. Every other Identity rule
/// (<c>Password.RequireDigit</c> and friends, <c>Lockout.AllowedForNewUsers</c>) stays on the
/// framework default.
/// </remarks>
public sealed class IdentityPolicyOptions
{
    public const string SectionName = "Authentication";

    /// <summary>How long a locked-out account stays locked, in minutes. Default 5, minimum 1.</summary>
    public int LockoutDurationMinutes { get; set; } = 5;

    /// <summary>Failed sign-in attempts before lockout. Default 5, minimum 1.</summary>
    public int LockoutMaxFailedAttempts { get; set; } = 5;

    /// <summary>Minimum password length. Default 8, minimum 1.</summary>
    public int PasswordMinimumLength { get; set; } = 8;
}
