namespace PointLurker.Core.Domain.Localization;

/// <summary>
/// Allowlist of BCP-47 locale tags the application supports.
/// Add new entries here when a new locale is introduced.
/// The default locale is <see cref="EnUs"/>.
/// </summary>
public static class SupportedLocales
{
    public const string EnUs = "en-US";
    public const string PtBr = "pt-BR";

    /// <summary>
    /// Ordered list of all supported locales. Used for validation and migration CHECK constraints.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [EnUs, PtBr];

    /// <summary>
    /// Returns true when the given locale tag is in the allowlist.
    /// Comparison is ordinal case-sensitive (BCP-47 tags have canonical casing).
    /// </summary>
    public static bool IsSupported(string locale) => All.Contains(locale, StringComparer.Ordinal);
}
