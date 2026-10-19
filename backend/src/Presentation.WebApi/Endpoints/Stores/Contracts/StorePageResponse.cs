using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The shared page envelope, carrying store rows. The active and paused totals are not here: they
/// are the same whatever the filter, so they are served by <c>GET /api/stores/summary</c> rather
/// than re-sent with every narrowed page.
/// </summary>
internal sealed record StorePageResponse(
    IReadOnlyList<StoreListItemResponse> Items,
    int TotalCount,
    int Offset,
    int Limit
)
{
    public static explicit operator StorePageResponse(WatchedStoreListResult result) =>
        new(
            [.. result.Items.Select(row => (StoreListItemResponse)row)],
            result.TotalCount,
            result.Offset,
            result.Limit
        );
}
