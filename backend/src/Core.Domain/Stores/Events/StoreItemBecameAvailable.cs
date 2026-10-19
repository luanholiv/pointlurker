using PointLurker.Core.Domain.Common;

namespace PointLurker.Core.Domain.Stores;

public sealed record StoreItemBecameAvailable(
    Guid StoreItemId,
    Guid WatchedStoreId,
    string ItemName,
    DateTimeOffset OccurredAt
) : IDomainEvent;
