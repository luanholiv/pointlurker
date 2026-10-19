using System.Globalization;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// The console's design tokens resolved to sRGB hex for use in email.
/// </summary>
/// <remarks>
/// The console defines its palette in OKLCH behind CSS custom properties. Mail clients support
/// neither, so each token is converted once and frozen here under the name it carries in the
/// stylesheet, which is what keeps the two traceable to each other.
/// <para>
/// These are the dark-theme values, because dark is what the console shows by default when a
/// visitor has no stored preference.
/// </para>
/// <para>
/// Tokens the stylesheet defines with alpha are pre-flattened over the surface they sit on —
/// named accordingly — since mail clients handle translucent borders inconsistently.
/// </para>
/// </remarks>
internal static class EmailPalette
{
    public const string Bg = "#0a0d14";
    public const string Panel = "#141822";
    public const string Card = "#1a1f2a";
    public const string Chip = "#212631";

    public const string Ink = "#eef2f9";
    public const string Ink2 = "#bfc4ce";
    public const string Ink3 = "#868c99";

    public const string LineSoftOnBg = "#171b23";
    public const string LineSoftOnCard = "#222732";

    public const string Accent = "#f9b73f";
    public const string AccentInk = "#22190a";

    public const string OkDot = "#61cb7c";
    public const string OkText = "#7cdd93";
    public const string OkSoft = "#253b37";

    public const string WarnDot = "#f5ae39";
    public const string WarnText = "#ffc469";
    public const string WarnSoft = "#41392d";

    /// <summary>Body copy stack. The console's webfonts are not loadable in mail, so the
    /// display and body faces lead a stack that degrades to whatever the client has.</summary>
    public const string FontBody =
        "'Inter Tight',-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    /// <summary>Headline stack, mirroring the console's display face.</summary>
    public const string FontDisplay =
        "'Bricolage Grotesque','Inter Tight',-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    /// <summary>Eyebrow, label and numeric stack, mirroring the console's mono face.</summary>
    public const string FontMono =
        "'JetBrains Mono',ui-monospace,SFMono-Regular,Menlo,Consolas,'Courier New',monospace";

    /// <summary>
    /// The item swatch fill for a given hue: the console's <c>oklch(0.42 0.12 h)</c>, which is
    /// the first stop of the gradient on an item card. Only the first stop is used because
    /// Outlook drops CSS gradients entirely, and a fill that disappears on one client is worse
    /// than a flat one everywhere.
    /// </summary>
    public static string SwatchHex(int hue) => OklchToHex(0.42, 0.12, hue);

    /// <summary>
    /// Converts an OKLCH color to an sRGB hex string, clipping out-of-gamut channels.
    /// </summary>
    /// <remarks>
    /// Oklab to linear sRGB via Björn Ottosson's published matrices, then the sRGB transfer
    /// function. Only ever fed the fixed lightness and chroma above, so every result is a
    /// saturated mid-tone that survives clipping without visible banding.
    /// </remarks>
    private static string OklchToHex(double lightness, double chroma, double hueDegrees)
    {
        var hueRadians = hueDegrees * Math.PI / 180.0;
        var a = chroma * Math.Cos(hueRadians);
        var b = chroma * Math.Sin(hueRadians);

        var l = Cube(lightness + (0.3963377774 * a) + (0.2158037573 * b));
        var m = Cube(lightness - (0.1055613458 * a) - (0.0638541728 * b));
        var s = Cube(lightness - (0.0894841775 * a) - (1.2914855480 * b));

        var red = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        var green = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        var blue = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{ToChannel(red):x2}{ToChannel(green):x2}{ToChannel(blue):x2}"
        );
    }

    private static double Cube(double value) => value * value * value;

    private static int ToChannel(double linear)
    {
        var clipped = Math.Clamp(linear, 0.0, 1.0);
        var encoded =
            clipped <= 0.0031308 ? 12.92 * clipped : (1.055 * Math.Pow(clipped, 1.0 / 2.4)) - 0.055;

        return (int)Math.Round(encoded * 255.0, MidpointRounding.AwayFromZero);
    }
}
