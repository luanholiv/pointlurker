using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreListItemResponse(
    Guid Id,
    string ChannelId,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    int ItemCount,
    string? AvatarUrl,
    /// <summary>The provider slug, which is also the catalog key the badge renders from.</summary>
    string Provider
)
{
    public static explicit operator StoreListItemResponse(WatchedStoreListRow row) =>
        new(
            row.Id,
            row.ExternalId,
            row.DisplayName,
            row.IsActive,
            row.LastSyncedAt,
            row.ItemCount,
            row.AvatarUrl,
            row.Provider.ToSlug()
        );
}
