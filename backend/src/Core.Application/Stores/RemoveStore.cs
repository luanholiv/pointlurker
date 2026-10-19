using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class RemoveStore(IWatchedStoreRepository storeRepository, IUnitOfWork unitOfWork)
{
    public async Task<Result<Unit, StoreError>> ExecuteAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        // Row only: the store's items and their wishlist entries go by the database's ON DELETE
        // CASCADE, so there is no reason to materialise the graph in order to throw it away.
        var store = await storeRepository.GetForUpdateAsync(storeId, ct);
        if (store is null)
        {
            return Result<Unit, StoreError>.Failure(StoreError.NotFound());
        }

        storeRepository.Remove(store);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Unit, StoreError>.Success(Unit.Value);
    }
}
