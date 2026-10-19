using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using static PointLurker.Infrastructure.ExternalServices.Email.EmailMarkup;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Renders the email warning that a provider credential expires soon, or already has, in the same
/// email-safe markup as <see cref="AvailabilityEmailTemplate"/>.
/// </summary>
/// <remarks>
/// The instant is written in UTC and says so: the owner's time zone is not known here, and a bare
/// time read as local would be off by hours.
/// </remarks>
public sealed class CredentialExpiryEmailTemplate(
    IStringLocalizer<EmailResources> localizer,
    IOptions<AppUrlSettings> appUrl
)
{
    private const string Prefix = "Email.CredentialExpiry.";

    // The brand line is the same one every email opens with, so it is read from the catalog entry
    // that already holds it rather than duplicated under a second key.
    private const string BrandPrefix = "Email.AvailabilityNotification.Brand.";

    private readonly string? _baseUrl = appUrl.Value.PublicBaseUrl;

    public string RenderSubject(CredentialExpiryEmailModel model) =>
        localizer[$"{Prefix}{State(model)}.Subject", Provider(model)].Value;

    public string RenderHtml(CredentialExpiryEmailModel model)
    {
        var provider = Provider(model);
        var expiresAt = model.ExpiresAt.UtcDateTime.ToString("f", CultureInfo.CurrentCulture);
        var sb = new StringBuilder();

        sb.Append(
            $"""
            <!DOCTYPE html>
            <html lang="{Encode(CultureInfo.CurrentUICulture.Name)}">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="dark">
            <meta name="supported-color-schemes" content="dark">
            <title>{Encode(RenderSubject(model))}</title>
            </head>
            <body style="margin:0;padding:0;background-color:{EmailPalette.Bg};">
            <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;">{Encode(
                localizer[$"{Prefix}{State(model)}.Preheader", provider, expiresAt].Value
            )}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{EmailPalette.Bg};">
            <tr><td align="center" style="padding:32px 16px;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:600px;max-width:600px;">
            <tr><td style="padding:0 0 18px;">
            <div style="font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.16em;text-transform:uppercase;color:{EmailPalette.Ink3};">{Encode(
                Upper(localizer[$"{BrandPrefix}Name"].Value)
            )}</div>
            <div style="font-family:{EmailPalette.FontDisplay};font-size:18px;line-height:1.1;letter-spacing:-0.01em;color:{EmailPalette.Ink};padding-top:2px;">{Encode(
                localizer[$"{BrandPrefix}Sub"].Value
            )}</div>
            </td></tr>
            <tr><td style="border-top:1px solid {EmailPalette.LineSoftOnBg};font-size:0;line-height:0;">&nbsp;</td></tr>
            <tr><td style="padding:26px 0 0;font-family:{EmailPalette.FontDisplay};font-size:32px;line-height:1.05;letter-spacing:-0.015em;color:{EmailPalette.Ink};">{Encode(
                localizer[$"{Prefix}{State(model)}.Intro", provider].Value
            )}</td></tr>
            <tr><td style="padding:12px 0 0;font-family:{EmailPalette.FontBody};font-size:14px;line-height:1.5;color:{EmailPalette.Ink2};">{Encode(
                localizer[$"{Prefix}{State(model)}.Detail", provider, expiresAt].Value
            )}</td></tr>
            <tr><td style="padding:14px 0 0;font-family:{EmailPalette.FontBody};font-size:13px;line-height:1.5;color:{EmailPalette.Ink3};">{Encode(
                localizer[$"{Prefix}Action", provider].Value
            )}</td></tr>
            """
        );

        AppendCallToAction(sb);

        sb.Append(
            """
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """
        );

        return sb.ToString();
    }

    /// <summary>
    /// The link to the settings page, where a credential is replaced. Rendered only when the
    /// deployment published a URL, as the availability email does.
    /// </summary>
    private void AppendCallToAction(StringBuilder sb)
    {
        if (_baseUrl is null)
        {
            return;
        }

        var href = Encode($"{_baseUrl}/settings");
        var label = Encode(localizer[$"{Prefix}Cta.OpenSettings"].Value);

        sb.Append(
            $"""
            <tr><td style="padding:22px 0 0;">
            <a href="{href}" style="display:inline-block;padding:12px 22px;background-color:{EmailPalette.Accent};color:{EmailPalette.AccentInk};font-family:{EmailPalette.FontBody};font-size:13px;font-weight:500;letter-spacing:-0.005em;text-decoration:none;border-radius:3px;">{label}</a>
            </td></tr>
            """
        );
    }

    private static string State(CredentialExpiryEmailModel model) =>
        model.HasExpired ? "Expired" : "Expiring";

    // A brand name, the same in every locale.
    private static string Provider(CredentialExpiryEmailModel model) => model.Provider.ToString();

    private static string Upper(string value) => value.ToUpper(CultureInfo.CurrentCulture);
}
