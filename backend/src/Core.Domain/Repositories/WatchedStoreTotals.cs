namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// How many of a caller's stores are active and how many are paused, across all of them and
/// independent of any filter. Served apart from the page for the same reason the item facets are:
/// a value that does not vary with the filter has no business being re-sent with every filtered
/// page, and the page envelope is the same shape at every endpoint.
/// </summary>
public sealed record WatchedStoreTotals(int ActiveCount, int PausedCount);
