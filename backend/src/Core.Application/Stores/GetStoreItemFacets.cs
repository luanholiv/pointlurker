using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Reads the values a store's filter controls offer. Takes no filter by design — see
/// <see cref="StoreItemFacets"/>.
/// </summary>
public sealed class GetStoreItemFacets(
    IWatchedStoreRepository storeRepository,
    IStoreItemRepository storeItemRepository
)
{
    public async Task<Result<StoreItemFacets, StoreError>> ExecuteAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        if (!await storeRepository.ExistsAsync(storeId, ct))
        {
            return Result<StoreItemFacets, StoreError>.Failure(StoreError.NotFound());
        }

        return Result<StoreItemFacets, StoreError>.Success(
            await storeItemRepository.GetFacetsAsync(storeId, ct)
        );
    }
}
