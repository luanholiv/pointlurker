using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// One row of the watched-store list, projected in SQL. <see cref="ItemCount"/> is counted by the
/// database rather than by loading the store's items — the list renders a number, not a collection.
/// </summary>
public sealed record WatchedStoreListRow(
    Guid Id,
    string ExternalId,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    int ItemCount,
    string? AvatarUrl,
    StoreProvider Provider
);
