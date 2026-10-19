using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class DeleteStoreProfileAlias(
    IWatchedStoreRepository storeRepository,
    IUnitOfWork unitOfWork
)
{
    /// <summary>
    /// Detaches a manual label mapping. Idempotent, like the value delete. Removing the alias
    /// does not necessarily leave the label unresolved — it may still fuzzy-match the canonical
    /// it was aliased to.
    /// </summary>
    public async Task<Result<Unit, StoreError>> ExecuteAsync(
        Guid storeId,
        string rawLabel,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetForUpdateAsync(storeId, ct);
        if (store is null)
        {
            return Result<Unit, StoreError>.Failure(StoreError.NotFound());
        }

        if (store.RemoveAlias(rawLabel))
        {
            storeRepository.Update(store);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result<Unit, StoreError>.Success(Unit.Value);
    }
}
