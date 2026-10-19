using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class ListWatchedStores(IWatchedStoreRepository storeRepository)
{
    public async Task<Result<WatchedStoreListResult, StoreError>> ExecuteAsync(
        Guid userId,
        WatchedStoreFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    )
    {
        var (rows, totalCount) = await storeRepository.ListByUserAsync(
            userId,
            filter,
            offset,
            limit,
            ct
        );

        return Result<WatchedStoreListResult, StoreError>.Success(
            new WatchedStoreListResult(rows, totalCount, offset, limit)
        );
    }
}
