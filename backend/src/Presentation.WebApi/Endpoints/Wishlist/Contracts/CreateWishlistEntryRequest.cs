namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

/// <summary>
/// <paramref name="DesiredRedemptions"/> defaults to 1 when the property is absent and means
/// unlimited when it is present and null. The default is deliberate: the field authorises
/// irreversible spending, so a body that forgets it must fail closed, and unlimited has to be
/// asked for.
/// </summary>
internal sealed record CreateWishlistEntryRequest(
    Guid StoreItemId,
    bool NotifyOnAvailable,
    bool AutoRedeem,
    string? RedemptionMessage,
    int? DesiredRedemptions = 1
);
