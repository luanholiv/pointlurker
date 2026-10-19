namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One wishlist entry in a store's group. <c>IsBlocked</c> and <c>MissingLabels</c> are computed
/// against the store's input profile as it stands now, not read from the entry's persisted flag, so the
/// list reflects a profile edit before the next sync cycle re-evaluates coverage.
/// <para>
/// <c>QuotaUsed</c> against <c>DesiredRedemptions</c> is how a satisfied entry is told apart from one
/// the user switched off: auto-redeem stays on when the quota is met, so the flag alone cannot say.
/// A null <c>DesiredRedemptions</c> is unlimited.
/// </para>
/// </summary>
public sealed record WishlistEntryGroupItem(
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
    string? ThumbnailUrl = null
);
