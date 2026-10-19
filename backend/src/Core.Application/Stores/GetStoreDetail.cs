using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Reads a store's envelope. The items are served by their own filtered, paged endpoint, so this
/// reads the envelope and an item count computed in SQL rather than materializing the collection to
/// measure it.
/// </summary>
public sealed class GetStoreDetail(IWatchedStoreRepository storeRepository)
{
    public async Task<Result<WatchedStoreDetailRow, StoreError>> ExecuteAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetDetailAsync(storeId, ct);
        if (store is null)
        {
            return Result<WatchedStoreDetailRow, StoreError>.Failure(StoreError.NotFound());
        }

        return Result<WatchedStoreDetailRow, StoreError>.Success(store);
    }
}
