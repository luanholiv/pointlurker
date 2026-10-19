namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// Narrowing and ordering for one watched store's item query. Every nullable member is a filter to
/// skip when null; <see cref="AvailableOnly" /> is not nullable because the grid's default view is
/// available-items-only, so "unset" already means <c>true</c> by the time the query is built.
/// </summary>
/// <param name="SearchTerm">
/// Free text matched against the item's name and description. Not a substring test: the term is
/// tokenized, so its words match in any order and independently of accents, and a name match
/// outranks a description match.
/// </param>
/// <param name="AvailableOnly">Restrict to items satisfying <c>StoreItem.IsAvailableExpression</c>.</param>
/// <param name="SubscriberOnly">Restrict to subscriber-only items when <c>true</c>; no narrowing when null.</param>
/// <param name="Featured">Restrict to featured items when <c>true</c>; no narrowing when null.</param>
/// <param name="Categories">Match any of the listed categories; no narrowing when null or empty.</param>
/// <param name="MinCost">Inclusive lower bound on cost.</param>
/// <param name="MaxCost">Inclusive upper bound on cost.</param>
public sealed record StoreItemFilter(
    string? SearchTerm,
    bool AvailableOnly,
    bool? SubscriberOnly,
    bool? Featured,
    IReadOnlyList<string>? Categories,
    long? MinCost,
    long? MaxCost,
    StoreItemSortField SortField,
    SortDirection SortDirection
);
