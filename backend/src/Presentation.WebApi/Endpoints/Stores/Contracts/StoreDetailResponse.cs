using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// A store's envelope. It carries no item collection — items are served filtered, sorted
/// and paged by <c>GET /api/stores/{id}/items</c>, so the page header renders while the grid loads
/// and a load-more does not re-serialize the store.
/// </summary>
/// <param name="ItemCount">
/// The store's <b>unfiltered</b> item count. The page needs it to tell an empty store from an
/// over-filtered one, and a filtered page total cannot answer that.
/// </param>
internal sealed record StoreDetailResponse(
    Guid Id,
    string ChannelId,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    int ItemCount,
    IReadOnlyList<string> MissingLabels,
    string? AvatarUrl,
    /// <summary>The provider slug, which is also the catalog key the badge renders from.</summary>
    string Provider
)
{
    /// <summary>
    /// Missing labels are not a property of the store — they are computed per request — so reading
    /// a store on its own reports none. Use the <see cref="AddWatchedStoreResult"/> conversion when
    /// the caller has them.
    /// </summary>
    public static explicit operator StoreDetailResponse(WatchedStoreDetailRow store) =>
        new(
            store.Id,
            store.ExternalId,
            store.DisplayName,
            store.IsActive,
            store.LastSyncedAt,
            store.CreatedAt,
            store.ItemCount,
            [],
            store.AvatarUrl,
            store.Provider.ToSlug()
        );

    /// <summary>
    /// The add-store path holds the freshly built aggregate rather than a projected row, so it
    /// counts the items it just inserted.
    /// </summary>
    public static explicit operator StoreDetailResponse(AddWatchedStoreResult result) =>
        new(
            result.Store.Id,
            result.Store.ExternalId.Value,
            result.Store.DisplayName,
            result.Store.IsActive,
            result.Store.LastSyncedAt,
            result.Store.CreatedAt,
            result.Store.Items.Count,
            result.MissingLabels,
            result.Store.AvatarUrl,
            result.Store.Provider.ToSlug()
        );
}
