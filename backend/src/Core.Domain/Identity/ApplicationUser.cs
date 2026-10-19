using Microsoft.AspNetCore.Identity;
using PointLurker.Core.Domain.Localization;

namespace PointLurker.Core.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// BCP-47 locale tag for this user. Defaults to <c>en-US</c>.
    /// Must be a value in <see cref="SupportedLocales.All"/>.
    /// </summary>
    public string Locale { get; private set; } = SupportedLocales.EnUs;

    /// <summary>
    /// Updates the user's preferred locale.
    /// The caller is responsible for validating against <see cref="SupportedLocales.All"/> before calling this.
    /// </summary>
    public void SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale cannot be empty.", nameof(locale));

        Locale = locale;
    }
}
