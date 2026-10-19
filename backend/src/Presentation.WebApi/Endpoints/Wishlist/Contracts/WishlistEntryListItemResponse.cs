using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

internal sealed record WishlistEntryListItemResponse(
    Guid Id,
    Guid StoreItemId,
    string ItemName,
    long ItemCost,
    bool IsAvailable,
    bool NotifyOnAvailable,
    bool AutoRedeem,
    int? DesiredRedemptions,
    int QuotaUsed,
    bool IsBlocked,
    IReadOnlyList<string> MissingLabels,
    DateTimeOffset CreatedAt,
    string? ThumbnailUrl
)
{
    public static explicit operator WishlistEntryListItemResponse(WishlistEntryGroupItem item) =>
        new(
            item.Id,
            item.StoreItemId,
            item.ItemName,
            item.ItemCost,
            item.IsAvailable,
            item.NotifyOnAvailable,
            item.AutoRedeem,
            item.DesiredRedemptions,
            item.QuotaUsed,
            item.IsBlocked,
            item.MissingLabels,
            item.CreatedAt,
            item.ThumbnailUrl
        );
}
