using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// A store's envelope, projected in SQL. <see cref="ItemCount"/> is the store's <b>unfiltered</b>
/// item count and is counted by the database rather than by materializing the items: the page it
/// feeds distinguishes "this store has no items" from "no items match the current filters", and a
/// filtered page total cannot answer the first question.
/// </summary>
public sealed record WatchedStoreDetailRow(
    Guid Id,
    string ExternalId,
    string DisplayName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    int ItemCount,
    string? AvatarUrl,
    StoreProvider Provider
);
