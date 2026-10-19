using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One slice of the watched-store list with the total matching the filter. The active and paused
/// totals are deliberately absent: they do not vary with the filter and are served separately
/// (see <see cref="WatchedStoreTotals"/>).
/// </summary>
public sealed record WatchedStoreListResult(
    IReadOnlyList<WatchedStoreListRow> Items,
    int TotalCount,
    int Offset,
    int Limit
);
