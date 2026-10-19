namespace PointLurker.Infrastructure.Database.Repositories;

/// <summary>
/// The rules one item search runs by. They live together because they only make sense together: the
/// configuration decides what counts as a word, the threshold decides what counts as a near miss,
/// and the minimum length decides when guessing at a near miss is worth doing at all. Split across
/// call sites, two of them could disagree about the same term.
/// </summary>
internal static class StoreItemTextSearch
{
    /// <summary>
    /// The text search configuration the search migration creates: <c>simple</c> plus
    /// <c>unaccent</c>. Deliberately not a language dictionary — item text is written by streamers
    /// and its language has nothing to do with the reader's locale. One observed store names items
    /// in English and describes them in Portuguese, in the same row, so there is no per-row language
    /// to stem by; a wrong stemmer is worse than none. Folding accents is not a convenience here
    /// either: the same word arrives both accented and bare across rows.
    /// </summary>
    internal const string Configuration = "item_search";

    /// <summary>
    /// How close a word has to be to count as the term misspelled rather than a different word.
    /// Postgres' own default for <c>word_similarity</c>; named here because the query states it
    /// outright instead of reading the session setting, so the same term cannot match differently
    /// depending on who connected.
    /// </summary>
    private const double FuzzyThreshold = 0.6;

    /// <summary>
    /// Terms shorter than this are matched exactly, never approximately. Three characters resemble
    /// too much to be evidence of anything — and it keeps a one-character term like <c>%</c> or
    /// <c>_</c>, which a user means literally, from dragging in rows that merely look alike.
    /// </summary>
    private const int MinimumFuzzyTermLength = 4;

    /// <summary>
    /// A similarity nothing can reach, since <c>word_similarity</c> returns at most 1.
    /// </summary>
    private const double Unreachable = 2;

    /// <summary>
    /// The similarity <paramref name="term"/> has to reach to count as a near miss, or a value no
    /// comparison can reach when guessing at one would do more harm than good.
    /// </summary>
    /// <remarks>
    /// Turning the arm off by raising its bar out of range, rather than by giving the query a second
    /// shape, is what lets the predicate and the ordering share one expression — and they have to,
    /// because the ordering asks "is the name why this row is here?" and would otherwise answer it
    /// with a rule the narrowing did not use.
    /// <para>
    /// Off for a term of more than one word. A misspelling is a property of a single word, while
    /// <c>word_similarity</c> scores the term against the word extents of the text, so the words it
    /// gets right carry the score: "Bulk Item 007" scores 0.857 against "Bulk Item 001", and one
    /// term would return the whole catalogue.
    /// </para>
    /// </remarks>
    internal static double NearMissThreshold(string term) =>
        term.Length >= MinimumFuzzyTermLength && !term.Any(char.IsWhiteSpace)
            ? FuzzyThreshold
            : Unreachable;
}
