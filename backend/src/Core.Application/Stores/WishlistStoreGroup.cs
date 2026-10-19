namespace PointLurker.Core.Application.Stores;

/// <summary>One store's wishlist entries, with the store detail the list renders as their heading.</summary>
public sealed record WishlistStoreGroup(
    Guid StoreId,
    string StoreName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    IReadOnlyList<WishlistEntryGroupItem> Entries,
    string? AvatarUrl = null
);
