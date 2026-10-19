using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// A code owed to the owner, with what its redemption snapshotted about the item. The names come
/// from the redemption rather than the store, so they stay right after the item is renamed or the
/// store is removed.
/// </summary>
/// <param name="Code">Tracked: the caller records the delivery outcome on it.</param>
public sealed record PendingFulfillmentDelivery(
    FulfillmentCode Code,
    Guid StoreItemId,
    string StoreItemName,
    string StoreName
);
