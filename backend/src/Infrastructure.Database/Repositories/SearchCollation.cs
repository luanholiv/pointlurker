namespace PointLurker.Infrastructure.Database.Repositories;

/// <summary>
/// The collation every name ordering uses. Root ICU, chosen to match the <c>localeCompare</c> the
/// client used: names order case-insensitively and accent-aware, so "arvore", "Árvore" and "Zebra"
/// land where a reader expects rather than where UTF-16 code points put them. One constant, so two
/// lists cannot end up ordering names differently from each other.
/// </summary>
internal static class SearchCollation
{
    internal const string Name = "und-x-icu";
}
