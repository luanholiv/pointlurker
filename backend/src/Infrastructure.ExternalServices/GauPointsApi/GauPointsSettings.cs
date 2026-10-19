using System.ComponentModel.DataAnnotations;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

public sealed class GauPointsSettings
{
    public const string SectionName = "GauPoints";

    /// <summary>Defaults to the live site, so an existing deployment needs no new setting.</summary>
    [Required, Url]
    public string BaseUrl { get; set; } = "https://gaupoints.com.br";

    /// <summary>
    /// The session cookie's name. Configurable so a rename upstream is a setting, not a release.
    /// Restricted to cookie-name characters, since it is written straight into a header.
    /// </summary>
    [Required, RegularExpression("^[A-Za-z0-9_-]{1,64}$")]
    public string SessionCookieName { get; set; } = "kp_session";
}
