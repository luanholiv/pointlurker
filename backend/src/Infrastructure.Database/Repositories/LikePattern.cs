namespace PointLurker.Infrastructure.Database.Repositories;

/// <summary>
/// Builds the <c>ILIKE</c> pattern for a user's search term. Every searchable list endpoint escapes
/// the same way, so the rule lives in one place: unescaped, a term of <c>%</c> matches every row,
/// which reads to a user as "search is broken" rather than as anything malicious.
/// </summary>
internal static class LikePattern
{
    /// <summary>The character passed to <c>ILIKE … ESCAPE</c>.</summary>
    internal const string EscapeCharacter = "\\";

    /// <summary>Wraps <paramref name="term"/> in wildcards, escaping the ones it contains itself.</summary>
    internal static string Containing(string term) => $"%{Escape(term)}%";

    private static string Escape(string term) =>
        term.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("%", EscapeCharacter + "%")
            .Replace("_", EscapeCharacter + "_");
}
