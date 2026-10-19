using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class ListStoreItems(
    IWatchedStoreRepository storeRepository,
    IStoreItemRepository storeItemRepository
)
{
    public async Task<Result<StoreItemListResult, StoreError>> ExecuteAsync(
        Guid storeId,
        StoreItemFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    )
    {
        // An unknown store is a 404, not an empty page: "this store has no matching items" and
        // "there is no such store" are different answers and the interface renders them differently.
        if (!await storeRepository.ExistsAsync(storeId, ct))
        {
            return Result<StoreItemListResult, StoreError>.Failure(StoreError.NotFound());
        }

        var (rows, totalCount) = await storeItemRepository.ListAsync(
            storeId,
            filter,
            offset,
            limit,
            ct
        );

        return Result<StoreItemListResult, StoreError>.Success(
            new StoreItemListResult(rows, totalCount, offset, limit)
        );
    }
}
