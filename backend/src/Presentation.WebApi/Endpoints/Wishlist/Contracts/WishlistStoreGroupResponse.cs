using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

internal sealed record WishlistStoreGroupResponse(
    Guid StoreId,
    string StoreName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    IReadOnlyList<WishlistEntryListItemResponse> Entries,
    string? AvatarUrl
)
{
    public static explicit operator WishlistStoreGroupResponse(WishlistStoreGroup group) =>
        new(
            group.StoreId,
            group.StoreName,
            group.IsActive,
            group.LastSyncedAt,
            [.. group.Entries.Select(entry => (WishlistEntryListItemResponse)entry)],
            group.AvatarUrl
        );
}
