namespace PointLurker.Core.Application.Stores;

/// <summary>
/// A wishlist entry that lost auto-redeem input coverage during a sync pass, carrying the labels
/// the store input profile no longer resolves.
/// </summary>
public sealed record BlockedEvent(
    Guid WishlistEntryId,
    Guid StoreItemId,
    string ItemName,
    string StoreName,
    IReadOnlyList<string> MissingLabels
);
