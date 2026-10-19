using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class DeleteStoreProfileValue(
    IWatchedStoreRepository storeRepository,
    IUnitOfWork unitOfWork
)
{
    /// <summary>
    /// Removes a stored value by canonical label. Idempotent: deleting a label the store never
    /// held is a success, so the caller can retry a failed delete without special-casing.
    /// </summary>
    public async Task<Result<Unit, StoreError>> ExecuteAsync(
        Guid storeId,
        string labelCanonical,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetForUpdateAsync(storeId, ct);
        if (store is null)
        {
            return Result<Unit, StoreError>.Failure(StoreError.NotFound());
        }

        if (store.RemoveValue(labelCanonical))
        {
            storeRepository.Update(store);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return Result<Unit, StoreError>.Success(Unit.Value);
    }
}
