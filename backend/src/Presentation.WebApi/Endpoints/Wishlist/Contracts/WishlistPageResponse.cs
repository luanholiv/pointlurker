using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

/// <summary>
/// The shared page envelope, where one row is a store group. The group shape is unchanged from the
/// <c>{ stores: [...] }</c> body this replaces — the envelope constrains the envelope, not the row —
/// so a store's fields are still sent once rather than repeated on each of its entries.
/// </summary>
internal sealed record WishlistPageResponse(
    IReadOnlyList<WishlistStoreGroupResponse> Items,
    int TotalCount,
    int Offset,
    int Limit
)
{
    public static explicit operator WishlistPageResponse(WishlistGroupedResponse grouped) =>
        new(
            [.. grouped.Items.Select(group => (WishlistStoreGroupResponse)group)],
            grouped.TotalCount,
            grouped.Offset,
            grouped.Limit
        );
}
