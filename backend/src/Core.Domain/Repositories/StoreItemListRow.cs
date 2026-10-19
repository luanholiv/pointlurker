using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// One row of a store's item grid, projected in SQL. It carries everything a card renders or acts
/// on — including <see cref="WishlistEntryId" />, which decides the wishlisted badge and whether
/// the card opens the add or the edit flow — so rendering never costs a second round trip.
/// </summary>
public sealed record StoreItemListRow(
    Guid Id,
    string ProviderItemId,
    string Name,
    string? Description,
    long Cost,
    string CategoryName,
    bool SubscriberOnly,
    bool Enabled,
    int QuantityCurrent,
    int QuantityTotal,
    bool AllowMessages,
    string? ThumbnailUrl,
    Guid? WishlistEntryId,
    DateTimeOffset? ProviderCreatedAt
)
{
    /// <summary>
    /// Derived here rather than in the query, through the same rule the domain applies, so the
    /// projection carries no second copy of it to drift. Narrowing still happens in SQL via
    /// <see cref="Stores.StoreItem.IsAvailableExpression" />; only the reported value is computed.
    /// </summary>
    public bool IsAvailable =>
        ItemAvailability
            .From(Enabled, new ItemQuantity(QuantityTotal, QuantityCurrent))
            .IsAvailable;
}
