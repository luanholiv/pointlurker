using System.ComponentModel.DataAnnotations;

namespace PointLurker.Infrastructure.Database.Security;

/// <summary>
/// Location of the ASP.NET Data Protection key ring, bound to the <c>DataProtection</c>
/// configuration section. Deliberately NOT named <c>DataProtectionOptions</c>, which collides with
/// ASP.NET Core's own <c>DataProtectionOptions</c>.
/// </summary>
public sealed class DataProtectionSettings
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Directory holding the key ring. The default is the container path the shipped
    /// <c>docker-compose.yml</c> mounts a volume at; <c>appsettings.Development.json</c> overrides
    /// it with a relative path for local runs. Losing this directory permanently breaks decryption
    /// of every stored provider credential and redemption input.
    /// </summary>
    [Required]
    public string KeyRingPath { get; set; } = "/app/.aspnet/DataProtection-Keys";
}
