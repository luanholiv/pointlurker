using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// How many of the caller's stores are active and how many are paused, across all of them. Counted
/// in the database rather than from the loaded list, which is what lets that list be filtered and
/// paged without the headline numbers changing underneath it.
/// </summary>
internal sealed record StoreSummaryResponse(int ActiveCount, int PausedCount)
{
    public static explicit operator StoreSummaryResponse(WatchedStoreTotals totals) =>
        new(totals.ActiveCount, totals.PausedCount);
}
