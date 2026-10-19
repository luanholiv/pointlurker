using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

internal sealed record CreateWishlistEntryResponse(
    Guid Id,
    Guid StoreItemId,
    string ItemName,
    string StoreName,
    bool NotifyOnAvailable,
    bool AutoRedeem,
    DateTimeOffset CreatedAt
)
{
    public static explicit operator CreateWishlistEntryResponse(AddWishlistEntrySuccess success) =>
        new(
            success.Entry.Id,
            success.Entry.StoreItemId,
            success.Item.Name,
            success.Item.WatchedStore?.DisplayName ?? string.Empty,
            success.Entry.NotifyOnAvailable,
            success.Entry.AutoRedeem,
            success.Entry.CreatedAt
        );
}
