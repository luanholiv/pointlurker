using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using static PointLurker.Infrastructure.ExternalServices.Email.EmailMarkup;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Renders the availability notification as the console's design language expressed in email-safe
/// markup: tables for layout, inline styles only, and the palette resolved to hex by
/// <see cref="EmailPalette"/>.
/// </summary>
/// <remarks>
/// Both methods read the ambient culture, which <see cref="SmtpEmailService"/> sets for the
/// recipient before it calls in. Rendering is kept here rather than in the send path so the layout
/// is testable without SMTP.
/// </remarks>
public sealed class AvailabilityEmailTemplate(
    IStringLocalizer<EmailResources> localizer,
    IOptions<AppUrlSettings> appUrl
)
{
    private const string Prefix = "Email.AvailabilityNotification.";

    // Null when the deployment publishes no console URL, which is the signal to render no anchors
    // at all rather than links that go nowhere.
    private readonly string? _baseUrl = appUrl.Value.PublicBaseUrl;

    /// <summary>Category glyphs, mirroring the console's item card so an item is marked the
    /// same way in the inbox and on screen.</summary>
    private static readonly Dictionary<string, string> CategoryGlyphs = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["AUDIO"] = "&#9834;",
        ["ASSINATURA"] = "&#9733;",
        ["PIX"] = "&#8383;",
        ["SKIN"] = "&#9670;",
        ["GIFT"] = "&#10070;",
        ["SUB"] = "&#9673;",
    };

    private const string DefaultGlyph = "&#9671;";
    private const string PillDot = "&#9679;";

    public string RenderSubject(AvailabilityEmailModel model)
    {
        var totalItems = model.Stores.Sum(s => s.Items.Count);
        return Plural("Subject", totalItems);
    }

    public string RenderHtml(AvailabilityEmailModel model)
    {
        var totalItems = model.Stores.Sum(s => s.Items.Count);
        var pendingItems = model.Stores.Sum(s => s.Items.Count(i => i.RedemptionPending));

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
            """
        );

        AppendPreheader(sb, totalItems);

        sb.Append(
            $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{EmailPalette.Bg};">
            <tr><td align="center" style="padding:32px 16px;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:600px;max-width:600px;">
            """
        );

        AppendBrand(sb);
        AppendHeadline(sb);
        AppendSummary(sb, totalItems, model.Stores.Count, pendingItems);
        AppendCallToAction(sb);

        foreach (var store in model.Stores)
        {
            AppendStore(sb, store);
        }

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
    /// The hidden line a mail client shows beside the subject. Without it the client falls back to
    /// the first visible text, which would repeat the headline the reader can already see.
    /// </summary>
    private void AppendPreheader(StringBuilder sb, int totalItems)
    {
        // The zero-width spaces stop the client padding the preview out with whatever follows.
        var padding = string.Concat(Enumerable.Repeat("&#8203;&nbsp;", 40));

        sb.Append(
            $"""
            <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;">{Encode(
                Plural("Preheader", totalItems)
            )}{padding}</div>
            """
        );
    }

    private void AppendBrand(StringBuilder sb)
    {
        sb.Append(
            $"""
            <tr><td style="padding:0 0 18px;">
            <div style="font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.16em;text-transform:uppercase;color:{EmailPalette.Ink3};">{Encode(
                Upper(Text("Brand.Name"))
            )}</div>
            <div style="font-family:{EmailPalette.FontDisplay};font-size:18px;line-height:1.1;letter-spacing:-0.01em;color:{EmailPalette.Ink};padding-top:2px;">{Encode(
                Text("Brand.Sub")
            )}</div>
            </td></tr>
            <tr><td style="border-top:1px solid {EmailPalette.LineSoftOnBg};font-size:0;line-height:0;">&nbsp;</td></tr>
            """
        );
    }

    private void AppendHeadline(StringBuilder sb)
    {
        sb.Append(
            $"""
            <tr><td style="padding:26px 0 0;font-family:{EmailPalette.FontDisplay};font-size:32px;line-height:1.05;letter-spacing:-0.015em;color:{EmailPalette.Ink};">{Encode(
                Text("Body.Intro")
            )}</td></tr>
            """
        );
    }

    /// <summary>
    /// The counts the old email never stated — the reader had to tally list items to learn how
    /// many of anything there were. Laid out as the console's KPI tiles.
    /// </summary>
    private void AppendSummary(StringBuilder sb, int totalItems, int storeCount, int pendingItems)
    {
        var tiles = new List<(string Label, int Value)>
        {
            (Plural("Kpi.Items", totalItems), totalItems),
            (Plural("Kpi.Stores", storeCount), storeCount),
        };

        // Only worth a tile when something actually auto-redeemed; an always-present zero reads
        // like a failure rather than an absence.
        if (pendingItems > 0)
        {
            tiles.Add((Text("Kpi.AutoRedeeming"), pendingItems));
        }

        var width = 100 / tiles.Count;

        sb.Append(
            """
            <tr><td style="padding:20px 0 0;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            """
        );

        for (var i = 0; i < tiles.Count; i++)
        {
            var (label, value) = tiles[i];
            var gutter = i < tiles.Count - 1 ? "padding-right:8px;" : string.Empty;

            sb.Append(
                $"""
                <td width="{width.ToString(
                    CultureInfo.InvariantCulture
                )}%" valign="top" style="{gutter}">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{EmailPalette.Card};border:1px solid {EmailPalette.LineSoftOnCard};border-radius:4px;">
                <tr><td style="padding:14px 16px;">
                <div style="font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.1em;text-transform:uppercase;color:{EmailPalette.Ink3};">{Encode(
                    Upper(label)
                )}</div>
                <div style="font-family:{EmailPalette.FontDisplay};font-size:30px;line-height:1;letter-spacing:-0.02em;color:{EmailPalette.Ink};padding-top:6px;">{Encode(
                    Number(value)
                )}</div>
                </td></tr>
                </table>
                </td>
                """
            );
        }

        sb.Append("</tr></table></td></tr>");
    }

    /// <summary>
    /// The button back into the console. Rendered only when the deployment published a URL —
    /// there is no honest fallback for a link whose destination is unknown.
    /// </summary>
    private void AppendCallToAction(StringBuilder sb)
    {
        if (_baseUrl is null)
        {
            return;
        }

        var href = Encode($"{_baseUrl}/wishlist");
        var label = Encode(Text("Cta.ViewWishlist"));

        // Outlook's word renderer ignores padding and background on an anchor, so it gets a VML
        // button instead and every other client gets the anchor.
        sb.Append(
            $"""
            <tr><td style="padding:22px 0 0;">
            <!--[if mso]>
            <v:roundrect xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" href="{href}" style="height:40px;v-text-anchor:middle;width:230px;" arcsize="8%" strokecolor="{EmailPalette.Accent}" fillcolor="{EmailPalette.Accent}">
            <w:anchorlock/>
            <center style="color:{EmailPalette.AccentInk};font-family:sans-serif;font-size:13px;font-weight:500;">{label}</center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-- -->
            <a href="{href}" style="display:inline-block;padding:12px 22px;background-color:{EmailPalette.Accent};color:{EmailPalette.AccentInk};font-family:{EmailPalette.FontBody};font-size:13px;font-weight:500;letter-spacing:-0.005em;text-decoration:none;border-radius:3px;">{label}</a>
            <!--<![endif]-->
            </td></tr>
            """
        );
    }

    private void AppendStore(StringBuilder sb, AvailabilityEmailStoreGroup store)
    {
        // Eyebrow plus a hairline that runs to the right margin, the way a panel head reads in
        // the console. Replaces the bare heading the old email used as a store separator.
        sb.Append(
            $"""
            <tr><td style="padding:30px 0 12px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
            <td style="font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.14em;text-transform:uppercase;color:{EmailPalette.Ink3};white-space:nowrap;padding-right:12px;">{Encode(
                store.StoreName
            )}</td>
            <td width="100%" style="border-top:1px solid {EmailPalette.LineSoftOnBg};font-size:0;line-height:0;">&nbsp;</td>
            </tr></table>
            </td></tr>
            """
        );

        foreach (var item in store.Items)
        {
            AppendItem(sb, item, store.WatchedStoreId);
        }
    }

    private void AppendItem(StringBuilder sb, AvailabilityEmailItem item, Guid watchedStoreId)
    {
        var swatch = EmailPalette.SwatchHex(ItemHue(item.ItemName, item.CategoryName));
        var glyph = CategoryGlyphs.GetValueOrDefault(item.CategoryName, DefaultGlyph);
        var name = Encode(item.ItemName);

        if (_baseUrl is not null)
        {
            var href = Encode($"{_baseUrl}/stores/{watchedStoreId}");
            name =
                $"""<a href="{href}" style="color:{EmailPalette.Ink};text-decoration:none;">{name}</a>""";
        }

        sb.Append(
            $"""
            <tr><td style="padding:0 0 8px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{EmailPalette.Card};border:1px solid {EmailPalette.LineSoftOnCard};border-radius:4px;">
            <tr>
            <td width="64" valign="top" style="padding:14px 0 14px 14px;">
            <table role="presentation" width="64" cellpadding="0" cellspacing="0" border="0" style="background-color:{swatch};border-radius:4px;">
            <tr><td height="64" align="center" valign="middle" style="height:64px;line-height:64px;font-family:{EmailPalette.FontDisplay};font-size:30px;color:{EmailPalette.Ink};">{glyph}</td></tr>
            </table>
            </td>
            <td valign="top" style="padding:14px;">
            <div style="font-family:{EmailPalette.FontDisplay};font-size:19px;line-height:1.1;letter-spacing:-0.01em;color:{EmailPalette.Ink};">{name}</div>
            <div style="font-family:{EmailPalette.FontMono};font-size:14px;letter-spacing:0.02em;color:{EmailPalette.Ink};padding-top:6px;">{Encode(
                Number(item.Cost)
            )} <span style="font-size:11px;color:{EmailPalette.Ink3};">{Encode(
                Text("Item.Points")
            )}</span></div>
            <div style="padding-top:10px;">
            """
        );

        AppendPill(
            sb,
            Text("Item.Available"),
            EmailPalette.OkText,
            EmailPalette.OkSoft,
            EmailPalette.OkDot
        );

        if (item.RedemptionPending)
        {
            AppendPill(
                sb,
                Text("Item.AutoRedeemPending"),
                EmailPalette.WarnText,
                EmailPalette.WarnSoft,
                EmailPalette.WarnDot
            );
        }

        sb.Append("""</div><div style="padding-top:8px;">""");

        AppendChip(sb, item.CategoryName);

        if (item.SubscriberOnly)
        {
            AppendChip(sb, Text("Item.SubOnly"));
        }

        // Unlimited stock is -1, and the console hides the line rather than printing it.
        if (item.QuantityCurrent != -1)
        {
            AppendChip(
                sb,
                Format("Item.Stock", Number(item.QuantityCurrent), Number(item.QuantityTotal))
            );
        }

        sb.Append("</div></td></tr></table></td></tr>");
    }

    private static void AppendPill(
        StringBuilder sb,
        string label,
        string textColor,
        string background,
        string dotColor
    )
    {
        sb.Append(
            $"""
            <span style="display:inline-block;margin:0 6px 6px 0;padding:4px 10px;border-radius:99px;background-color:{background};color:{textColor};font-family:{EmailPalette.FontMono};font-size:10.5px;letter-spacing:0.08em;text-transform:uppercase;"><span style="color:{dotColor};">{PillDot}</span>&nbsp;{Encode(
                Upper(label)
            )}</span>
            """
        );
    }

    private static void AppendChip(StringBuilder sb, string label)
    {
        sb.Append(
            $"""
            <span style="display:inline-block;margin:0 6px 6px 0;padding:4px 10px;border-radius:3px;background-color:{EmailPalette.Chip};border:1px solid {EmailPalette.LineSoftOnCard};color:{EmailPalette.Ink2};font-family:{EmailPalette.FontMono};font-size:11px;letter-spacing:0.06em;text-transform:uppercase;">{Encode(
                Upper(label)
            )}</span>
            """
        );
    }

    /// <summary>
    /// Mirrors <c>itemHue</c> on the console's item card: same seed, same multiplier, same
    /// modulus, so an item's swatch is the same color in the inbox as on screen.
    /// </summary>
    /// <remarks>
    /// JavaScript's <c>charCodeAt</c> and a C# <c>char</c> are both UTF-16 code units, so the two
    /// implementations agree character for character. The modulus bounds the accumulator well
    /// below <see cref="int"/> range, so it cannot overflow.
    /// </remarks>
    private static int ItemHue(string name, string category)
    {
        var hue = 0;

        foreach (var c in name)
        {
            hue = ((hue * 31) + c) % 360;
        }

        foreach (var c in category)
        {
            hue = ((hue * 31) + c) % 360;
        }

        return hue;
    }

    /// <summary>
    /// Picks the <c>.One</c> or <c>.Other</c> form of a resource. Both supported locales have
    /// exactly two plural forms; a locale with more would need this widened alongside its catalog.
    /// </summary>
    private string Plural(string resourceName, int count) =>
        localizer[$"{Prefix}{resourceName}.{(count == 1 ? "One" : "Other")}", count].Value;

    private string Text(string resourceName) => localizer[$"{Prefix}{resourceName}"].Value;

    private string Format(string resourceName, params object[] args) =>
        localizer[$"{Prefix}{resourceName}", args].Value;

    // Pills and chips are uppercased here as well as in CSS: text-transform is unreliable in
    // several clients, and a lowercase label in a letterspaced mono pill reads as a defect.
    private static string Upper(string value) => value.ToUpper(CultureInfo.CurrentCulture);

    private static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
