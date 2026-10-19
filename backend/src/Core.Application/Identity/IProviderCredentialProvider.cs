using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Reads and stages the owner's <see cref="ProviderCredential"/>s. Credentials live on the Identity
/// context beside the user they belong to, so there is no commit here:
/// <see cref="ISingleUserProvider.SaveAsync"/> commits that context, and every change staged
/// through this port goes with it in the same transaction.
/// </summary>
public interface IProviderCredentialProvider
{
    /// <summary>The user's credential for <paramref name="provider"/>, tracked for update.</summary>
    Task<ProviderCredential?> GetAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct = default
    );

    /// <summary>Every credential the user holds, tracked for update, ordered by provider.</summary>
    Task<IReadOnlyList<ProviderCredential>> ListAsync(Guid userId, CancellationToken ct = default);

    void Add(ProviderCredential credential);

    void Remove(ProviderCredential credential);
}
