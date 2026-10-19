using PointLurker.Core.Domain.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreItemDetailResponse(
    Guid Id,
    string ProviderItemId,
    string Name,
    string? Description,
    string Type,
    long Cost,
    string CategoryName,
    bool Enabled,
    bool Featured,
    bool IsPublic,
    bool SubscriberOnly,
    bool AllowMessages,
    IReadOnlyList<string> UserInputDefinitions,
    int QuantityTotal,
    int QuantityCurrent,
    bool IsAvailable,
    Guid? WishlistEntryId,
    string? ThumbnailUrl,
    string? PreviewUrl
)
{
    public static explicit operator StoreItemDetailResponse(StoreItem item) =>
        new(
            item.Id,
            item.ProviderItemId.Value,
            item.Name,
            item.Description,
            item.Type,
            item.Cost,
            item.CategoryName,
            item.Enabled,
            item.Featured,
            item.Public,
            item.SubscriberOnly,
            item.AllowMessages,
            item.UserInputDefinitions,
            item.Quantity.Total,
            item.Quantity.Current,
            item.Availability.IsAvailable,
            // Null unless the caller loaded the WishlistEntry navigation.
            item.WishlistEntry?.Id,
            item.ThumbnailUrl,
            item.PreviewUrl
        );
}
