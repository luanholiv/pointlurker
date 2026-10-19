namespace PointLurker.Core.Application.Stores;

/// <summary>
/// What one store contributed to a sync cycle. Returned rather than accumulated into shared state,
/// so <see cref="StoreSyncUnit"/> stays free of cross-store synchronisation and the coordinator does
/// the aggregating once the parallel pass is over.
/// </summary>
/// <param name="ItemsProcessed">
/// How many item rows the pass touched: every item the payload carried, plus every local item that
/// has dropped out of it.
/// </param>
/// <param name="ItemsChanged">
/// How many of those actually moved. A sync re-reads the whole catalogue every tick, so this is
/// almost always far smaller — and it is the one that answers "did this cycle do anything".
/// </param>
public sealed record StoreSyncOutcome(
    int ItemsProcessed,
    int ItemsChanged,
    IReadOnlyList<AvailabilityEvent> AvailabilityEvents,
    IReadOnlyList<BlockedEvent> BlockedEvents
);
