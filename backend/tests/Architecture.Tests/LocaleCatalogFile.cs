using System.Text.Json;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A section of the frontend's locale catalog, read as dotted key paths mapped to the sentence each
/// one renders. The guards that compare a backend taxonomy against the catalog need both halves: the
/// key to tell a missing sentence from a present one, and the text to tell a sentence that
/// interpolates a value from one that only promises to.
/// <para>
/// The section is a path from the catalog root, so the two families read through here are addressed
/// as <c>errors</c> and <c>redemption.status</c>. Mind that they are unrelated nodes despite the
/// similar spelling: <c>errors.redemption.failure.*</c> lives under the first, and the status labels
/// under the second — <c>redemption.status</c>, never <c>errors.redemption.status</c>.
/// </para>
/// </summary>
internal static class LocaleCatalogFile
{
    /// <summary>Where the locale catalogs live, relative to the repository root.</summary>
    public const string CatalogRoot = "frontend/src/i18n/locales";

    /// <summary>
    /// The messages under <paramref name="section"/>, keyed by the dotted path below it —
    /// <c>stores.not_found</c> for <c>errors.stores.not_found</c>. Only leaves count: an
    /// intermediate object is a namespace, not a message.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Messages(string catalogFile, string section)
    {
        var path = SourcePaths.RepositoryFile($"{CatalogRoot}/{catalogFile}");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var element = Resolve(document.RootElement, section, catalogFile);
        var messages = new Dictionary<string, string>(StringComparer.Ordinal);
        Collect(element, prefix: string.Empty, messages);

        if (messages.Count == 0)
        {
            throw new InvalidOperationException(
                $"No messages found under `{section}` in {CatalogRoot}/{catalogFile}. "
                    + "A guard handed an empty catalog finds no violations, which reads as a pass."
            );
        }

        return messages;
    }

    /// <summary>The dotted key paths alone, for the guards that only compare names.</summary>
    public static IReadOnlySet<string> Keys(string catalogFile, string section) =>
        Messages(catalogFile, section).Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Walks the section path one segment at a time, naming the segment that is missing. Letting
    /// <see cref="JsonElement.GetProperty(string)"/> raise instead would report only the leaf name,
    /// which does not say whether the catalog moved the node or the caller spelled the path wrong.
    /// </summary>
    private static JsonElement Resolve(JsonElement root, string section, string catalogFile)
    {
        var element = root;
        var walked = new List<string>();

        foreach (var segment in section.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"`{string.Join('.', walked)}` in {CatalogRoot}/{catalogFile} is a "
                        + $"{element.ValueKind}, not an object, so `{section}` cannot be resolved."
                );
            }

            if (!element.TryGetProperty(segment, out element))
            {
                walked.Add(segment);
                throw new InvalidOperationException(
                    $"`{string.Join('.', walked)}` was not found in {CatalogRoot}/{catalogFile} "
                        + $"while resolving `{section}`. The catalog layout moved: update the "
                        + "guard so it keeps reading a real section rather than passing vacuously."
                );
            }

            walked.Add(segment);
        }

        return element;
    }

    private static void Collect(
        JsonElement element,
        string prefix,
        Dictionary<string, string> messages
    )
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            messages[prefix] = element.GetString() ?? string.Empty;
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var next = prefix.Length == 0 ? property.Name : $"{prefix}.{property.Name}";
            Collect(property.Value, next, messages);
        }
    }
}
