namespace PointLurker.Core.Application.Stores;

/// <summary>
/// A wishlisted item that became available during a sync pass. Collected per store and drained
/// once at the end of the run so a single notification can cover every store.
/// </summary>
public sealed record AvailabilityEvent(
    Guid StoreItemId,
    Guid WatchedStoreId,
    string ItemName,
    string StoreName
);
