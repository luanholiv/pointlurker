using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One slice of a store's item grid together with the total number of matching items and the bounds
/// that produced the slice. The total is what tells a client whether more items remain without
/// issuing another request.
/// </summary>
public sealed record StoreItemListResult(
    IReadOnlyList<StoreItemListRow> Items,
    int TotalCount,
    int Offset,
    int Limit
);
