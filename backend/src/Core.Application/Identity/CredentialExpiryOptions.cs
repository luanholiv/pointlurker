namespace PointLurker.Core.Application.Identity;

public sealed class CredentialExpiryOptions
{
    public const string SectionName = "Credentials";

    /// <summary>
    /// How far ahead of a credential's own expiry the owner is warned by email.
    /// </summary>
    /// <remarks>
    /// Checked on the provider account refresh schedule, which is daily by default. Two days rather
    /// than one gives two passes inside the window, so one failed email is not the only chance.
    /// </remarks>
    public int ExpiryWarningHours { get; set; } = 48;
}
