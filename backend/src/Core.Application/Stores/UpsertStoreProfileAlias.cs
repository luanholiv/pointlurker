using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class UpsertStoreProfileAlias(
    IWatchedStoreRepository storeRepository,
    IUnitOfWork unitOfWork
)
{
    public async Task<Result<Unit, StoreError>> ExecuteAsync(
        Guid storeId,
        string rawLabel,
        string canonicalLabel,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetForUpdateAsync(storeId, ct);
        if (store is null)
        {
            return Result<Unit, StoreError>.Failure(StoreError.NotFound());
        }

        store.UpsertAlias(rawLabel, canonicalLabel);
        storeRepository.Update(store);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Unit, StoreError>.Success(Unit.Value);
    }
}
