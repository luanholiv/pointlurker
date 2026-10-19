using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using static PointLurker.Infrastructure.ExternalServices.Email.EmailMarkup;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Renders the email that delivers one gift-card code, in the same email-safe markup as
/// <see cref="AvailabilityEmailTemplate"/>.
/// </summary>
/// <remarks>
/// The code appears in the body only. The subject and the preheader are what a lock screen or a
/// notification preview shows, so neither carries it.
/// </remarks>
public sealed class FulfillmentCodeEmailTemplate(
    IStringLocalizer<EmailResources> localizer,
    IOptions<AppUrlSettings> appUrl
)
{
    private const string Prefix = "Email.FulfillmentCode.";

    // The brand line is the same one every email opens with, so it is read from the catalog entry
    // that already holds it rather than duplicated under a second key.
    private const string BrandPrefix = "Email.AvailabilityNotification.Brand.";

    private readonly string? _baseUrl = appUrl.Value.PublicBaseUrl;

    public string RenderSubject(FulfillmentCodeEmailModel model) =>
        localizer[$"{Prefix}Subject", model.ItemName].Value;

    public string RenderHtml(FulfillmentCodeEmailModel model)
    {
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
                localizer[$"{Prefix}Preheader", model.ItemName, model.StoreName].Value
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
                Text("Body.Intro")
            )}</td></tr>
            <tr><td style="padding:12px 0 0;font-family:{EmailPalette.FontBody};font-size:14px;line-height:1.5;color:{EmailPalette.Ink2};">{Encode(
                localizer[$"{Prefix}Body.Detail", model.ItemName, model.StoreName].Value
            )}</td></tr>
            <tr><td style="padding:20px 0 0;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{EmailPalette.Card};border:1px solid {EmailPalette.LineSoftOnCard};border-radius:4px;">
            <tr><td style="padding:16px 18px;">
            <div style="font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.1em;text-transform:uppercase;color:{EmailPalette.Ink3};">{Encode(
                Upper(Text("Code.Label"))
            )}</div>
            <div style="font-family:{EmailPalette.FontMono};font-size:22px;line-height:1.3;letter-spacing:0.04em;color:{EmailPalette.Ink};padding-top:8px;word-break:break-all;">{Encode(
                model.Code
            )}</div>
            </td></tr>
            </table>
            </td></tr>
            <tr><td style="padding:14px 0 0;font-family:{EmailPalette.FontBody};font-size:13px;line-height:1.5;color:{EmailPalette.Ink3};">{Encode(
                Text("Body.Keep")
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
    /// The link to the redemption history. Rendered only when the deployment published a URL, as
    /// the availability email does.
    /// </summary>
    private void AppendCallToAction(StringBuilder sb)
    {
        if (_baseUrl is null)
        {
            return;
        }

        var href = Encode($"{_baseUrl}/redemptions");
        var label = Encode(Text("Cta.ViewRedemptions"));

        sb.Append(
            $"""
            <tr><td style="padding:22px 0 0;">
            <a href="{href}" style="display:inline-block;padding:12px 22px;background-color:{EmailPalette.Accent};color:{EmailPalette.AccentInk};font-family:{EmailPalette.FontBody};font-size:13px;font-weight:500;letter-spacing:-0.005em;text-decoration:none;border-radius:3px;">{label}</a>
            </td></tr>
            """
        );
    }

    private string Text(string resourceName) => localizer[$"{Prefix}{resourceName}"].Value;

    private static string Upper(string value) => value.ToUpper(CultureInfo.CurrentCulture);
}
