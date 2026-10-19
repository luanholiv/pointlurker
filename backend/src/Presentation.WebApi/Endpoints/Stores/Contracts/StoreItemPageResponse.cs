using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The shared page envelope, carrying item rows. It declares exactly <c>Items</c>,
/// <c>TotalCount</c>, <c>Offset</c> and <c>Limit</c> and nothing else — the facets deliberately live
/// at their own endpoint so this shape does not vary by offset and the category list is not
/// re-shipped on every load-more.
/// </summary>
internal sealed record StoreItemPageResponse(
    IReadOnlyList<StoreItemRowResponse> Items,
    int TotalCount,
    int Offset,
    int Limit
)
{
    public static explicit operator StoreItemPageResponse(StoreItemListResult result) =>
        new(
            [.. result.Items.Select(row => (StoreItemRowResponse)row)],
            result.TotalCount,
            result.Offset,
            result.Limit
        );
}
