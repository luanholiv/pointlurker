using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

internal sealed record WishlistEntryDetailResponse(
    Guid Id,
    Guid StoreItemId,
    string ItemName,
    long ItemCost,
    bool IsAvailable,
    string StoreName,
    Guid StoreId,
    bool NotifyOnAvailable,
    bool AutoRedeem,
    int? DesiredRedemptions,
    int QuotaUsed,
    bool AllowMessages,
    string? RedemptionMessage,
    bool IsBlocked,
    IReadOnlyList<string> MissingLabels,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
)
{
    public static explicit operator WishlistEntryDetailResponse(GetWishlistEntryResult entry) =>
        new(
            entry.Id,
            entry.StoreItemId,
            entry.ItemName,
            entry.ItemCost,
            entry.IsAvailable,
            entry.StoreName,
            entry.StoreId,
            entry.NotifyOnAvailable,
            entry.AutoRedeem,
            entry.DesiredRedemptions,
            entry.QuotaUsed,
            entry.AllowMessages,
            entry.RedemptionMessage,
            entry.IsBlocked,
            entry.MissingLabels,
            entry.CreatedAt,
            entry.UpdatedAt
        );
}
