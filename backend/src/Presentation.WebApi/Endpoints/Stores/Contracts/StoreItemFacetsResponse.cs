using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The values a store's filter controls offer, computed across every one of its items. Served apart
/// from the item pages: riding along in each page would re-ship the whole category list on every
/// load-more and make the page envelope vary by offset.
/// </summary>
internal sealed record StoreItemFacetsResponse(
    IReadOnlyList<string> Categories,
    long MinCost,
    long MaxCost
)
{
    public static explicit operator StoreItemFacetsResponse(StoreItemFacets facets) =>
        new(facets.Categories, facets.MinCost, facets.MaxCost);
}
