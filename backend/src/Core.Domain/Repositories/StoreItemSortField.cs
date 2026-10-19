namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The orderings a store's item grid offers. Member names match the <c>sort</c> query-string
/// values the frontend already writes to the browser URL.
/// </summary>
public enum StoreItemSortField
{
    Name,
    Cost,

    /// <summary>Remaining quantity, where <c>-1</c> means unlimited and orders after every finite count.</summary>
    Stock,

    /// <summary>The item's upstream creation timestamp, as its provider reports it.</summary>
    Date,

    /// <summary>
    /// How well the item answers <c>StoreItemFilter.SearchTerm</c>. Always best-first, so the sort
    /// direction does not apply to it — nobody asks for the worst matches. With no term there is
    /// nothing to rank and this orders by name instead, rather than refusing a value the enum
    /// accepts.
    /// </summary>
    Relevance,
}
