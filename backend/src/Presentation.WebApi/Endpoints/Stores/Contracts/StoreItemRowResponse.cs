using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// One card in a store's item grid. It carries everything the card renders or acts on — including
/// <see cref="WishlistEntryId"/>, which decides the wishlisted badge and whether the card opens the
/// add or the edit flow — so no card costs a second request.
/// </summary>
internal sealed record StoreItemRowResponse(
    Guid Id,
    string ProviderItemId,
    string Name,
    string? Description,
    long Cost,
    string CategoryName,
    bool SubscriberOnly,
    bool IsAvailable,
    int QuantityCurrent,
    int QuantityTotal,
    bool AllowMessages,
    string? ThumbnailUrl,
    Guid? WishlistEntryId,
    DateTimeOffset? ProviderCreatedAt
)
{
    public static explicit operator StoreItemRowResponse(StoreItemListRow row) =>
        new(
            row.Id,
            row.ProviderItemId,
            row.Name,
            row.Description,
            row.Cost,
            row.CategoryName,
            row.SubscriberOnly,
            row.IsAvailable,
            row.QuantityCurrent,
            row.QuantityTotal,
            row.AllowMessages,
            row.ThumbnailUrl,
            row.WishlistEntryId,
            row.ProviderCreatedAt
        );
}
