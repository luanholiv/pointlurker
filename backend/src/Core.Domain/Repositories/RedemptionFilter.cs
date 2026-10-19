using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// Narrowing and ordering for a redemption history query; every nullable member is a filter to skip
/// when null. The ordering applies to the whole matching result set before it is sliced — the page
/// this replaces sorted only the twenty rows it had already been given.
/// </summary>
public sealed record RedemptionFilter(
    RedemptionStatus? Status,
    Guid? WishlistEntryId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    RedemptionSortField SortField = RedemptionSortField.AttemptedAt,
    SortDirection SortDirection = SortDirection.Desc
);
