namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The values a store's filter controls offer, computed across <b>all</b> of that store's items and
/// deliberately ignoring the active filter — scoping these to the filtered set instead would make
/// the category list shrink as the user picks from it.
/// </summary>
/// <param name="Categories">Every category present in the store, in a stable order.</param>
/// <param name="MinCost">Cheapest item in the store, or <c>0</c> when the store holds none.</param>
/// <param name="MaxCost">Dearest item in the store, or <c>0</c> when the store holds none.</param>
public sealed record StoreItemFacets(IReadOnlyList<string> Categories, long MinCost, long MaxCost);
