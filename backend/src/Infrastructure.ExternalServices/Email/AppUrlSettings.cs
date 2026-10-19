namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Where this deployment's console is reachable from outside. An outbound email builds the links
/// it points back at from this value.
/// </summary>
/// <remarks>
/// Optional on purpose. A deployment that never sets it still gets a complete email — the
/// template renders the same content without anchors — so an existing install keeps working
/// across an upgrade instead of failing startup on a value it has never supplied.
/// </remarks>
public sealed class AppUrlSettings
{
    public const string SectionName = "App";

    /// <summary>
    /// Scheme and host, optionally a port: <c>https://monitor.example.com</c>. No trailing
    /// slash, path, query or fragment — <see cref="AppUrlSettingsValidator"/> enforces that so
    /// link building stays plain concatenation.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
