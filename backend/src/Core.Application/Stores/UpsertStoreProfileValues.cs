using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class UpsertStoreProfileValues(
    IWatchedStoreRepository storeRepository,
    [FromKeyedServices(ProtectionPurposes.StoreInputProfile)] ISecretProtector protector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
)
{
    /// <summary>
    /// Maximum length of a stored profile value (plaintext). Enforced by the request validator,
    /// which reports it per-field as <c>validation.too_long</c>; declared here because this is the
    /// use case the bound belongs to.
    /// </summary>
    public const int MaxValueLength = 1024;

    public async Task<Result<Unit, StoreError>> ExecuteAsync(
        Guid storeId,
        IReadOnlyList<StoreProfileValueUpsert> values,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetForUpdateAsync(storeId, ct);
        if (store is null)
        {
            return Result<Unit, StoreError>.Failure(StoreError.NotFound());
        }

        var now = timeProvider.GetUtcNow();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value.LabelCanonical))
            {
                continue;
            }
            if (value.Value is null)
            {
                store.RemoveValue(value.LabelCanonical);
                continue;
            }
            var ciphertext = protector.Protect(value.Value);
            store.UpsertValue(value.LabelCanonical, ciphertext, now);
        }

        // One commit for the whole batch: a partially applied profile edit is worse than none.
        storeRepository.Update(store);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Unit, StoreError>.Success(Unit.Value);
    }
}

public sealed record StoreProfileValueUpsert(string LabelCanonical, string? Value);
