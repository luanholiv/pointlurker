namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

/// <summary>
/// <paramref name="DesiredRedemptions"/> follows the same rule as on creation: absent means 1,
/// present and null means unlimited. A PUT replaces the entry's settings whole, so omitting it
/// resets the count exactly as omitting a flag resets that flag.
/// </summary>
internal sealed record UpdateWishlistEntryRequest(
    bool NotifyOnAvailable,
    bool AutoRedeem,
    string? RedemptionMessage,
    int? DesiredRedemptions = 1
);
