using System.Text.RegularExpressions;
using System.Xml.Linq;
using PointLurker.Core.Domain.Localization;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// The email catalogs are the one place the backend ships user-facing prose, and the only localized
/// resource with no compile-time tie between a key and its translations. A locale missing a key
/// falls back to English mid-message; one that drops a placeholder loses the number the sentence was
/// built around. Neither shows up in a build or in a render test that only exercises the default.
/// </summary>
public sealed class EmailResourceParityTests
{
    private const string ResourceRoot = "src/Infrastructure.ExternalServices/Resources/Email";
    private const string DefaultCatalog = "EmailResources.resx";

    /// <summary>
    /// Every supported locale bar the default, paired with its catalog file. Driving this off
    /// <see cref="SupportedLocales"/> rather than a literal list is what makes adding a locale
    /// fail here until its catalog exists, which is the step prose alone has never enforced.
    /// </summary>
    public static TheoryData<string, string> TranslatedCatalogs()
    {
        var data = new TheoryData<string, string>();

        foreach (var locale in SupportedLocales.All.Where(l => l != SupportedLocales.EnUs))
        {
            data.Add(locale, $"EmailResources.{locale}.resx");
        }

        return data;
    }

    [Fact]
    public void Default_catalog_is_not_empty()
    {
        var count = Read(DefaultCatalog).Count;

        // A parity guard handed two empty catalogs finds no violations, which reads as a pass.
        Assert.True(
            count > 0,
            $"""
            {nameof(Default_catalog_is_not_empty)}: no entries were read from
            {ResourceRoot}/{DefaultCatalog}. Either the file moved or its shape changed, and every
            other guard in this class is now passing vacuously.
            """
        );
    }

    [Theory]
    [MemberData(nameof(TranslatedCatalogs))]
    public void Every_locale_carries_exactly_the_default_catalogs_keys(string locale, string file)
    {
        var expected = Read(DefaultCatalog).Keys.ToHashSet(StringComparer.Ordinal);
        var actual = Read(file).Keys.ToHashSet(StringComparer.Ordinal);

        var missing = expected.Except(actual).Order(StringComparer.Ordinal).ToArray();
        var stale = actual.Except(expected).Order(StringComparer.Ordinal).ToArray();

        Assert.True(
            missing.Length == 0 && stale.Length == 0,
            $"""
            {nameof(Every_locale_carries_exactly_the_default_catalogs_keys)}: {ResourceRoot}/{file}
            ({locale}) does not match {DefaultCatalog}. A missing key silently renders the English
            string inside an otherwise translated message; a stale one is dead weight no code reads.
            Missing:
            {Join(missing)}
            Stale:
            {Join(stale)}
            """
        );
    }

    [Theory]
    [MemberData(nameof(TranslatedCatalogs))]
    public void Every_translation_keeps_the_placeholders_of_the_string_it_translates(
        string locale,
        string file
    )
    {
        var defaults = Read(DefaultCatalog);
        var translations = Read(file);

        var mismatched = defaults
            .Where(entry => translations.ContainsKey(entry.Key))
            .Where(entry =>
                !Placeholders(translations[entry.Key]).SetEquals(Placeholders(entry.Value))
            )
            .Select(entry =>
                $"{entry.Key}: expected {Join(Placeholders(entry.Value))}, "
                + $"found {Join(Placeholders(translations[entry.Key]))}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            mismatched.Length == 0,
            $"""
            {nameof(Every_translation_keeps_the_placeholders_of_the_string_it_translates)}:
            {ResourceRoot}/{file} ({locale}) interpolates different values than the default.
            A dropped placeholder loses the count the sentence was written around; an extra one
            throws on format.
            {Join(mismatched)}
            """
        );
    }

    [Theory]
    [MemberData(nameof(TranslatedCatalogs))]
    public void No_catalog_entry_is_blank(string locale, string file)
    {
        var blank = Read(file)
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            blank.Length == 0,
            $"""
            {nameof(No_catalog_entry_is_blank)}: {blank.Length} entr(ies) in
            {ResourceRoot}/{file} ({locale}) render nothing. An empty string reaches the recipient
            as a gap in the message rather than as a fallback.
            {Join(blank)}
            """
        );
    }

    private static IReadOnlyDictionary<string, string> Read(string file)
    {
        var path = SourcePaths.BackendFile($"{ResourceRoot}/{file}");

        return XDocument
            .Load(path)
            .Root!.Elements("data")
            .ToDictionary(
                data => data.Attribute("name")!.Value,
                data => data.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal
            );
    }

    private static HashSet<string> Placeholders(string text) =>
        Regex
            .Matches(text, @"\{\d+\}")
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string Join(IEnumerable<string> values) =>
        string.Join("\n", values.Order(StringComparer.Ordinal));
}
