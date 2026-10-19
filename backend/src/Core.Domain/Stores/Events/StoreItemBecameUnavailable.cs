using PointLurker.Core.Domain.Common;

namespace PointLurker.Core.Domain.Stores;

public sealed record StoreItemBecameUnavailable(
    Guid StoreItemId,
    Guid WatchedStoreId,
    string ItemName,
    DateTimeOffset OccurredAt
) : IDomainEvent;
