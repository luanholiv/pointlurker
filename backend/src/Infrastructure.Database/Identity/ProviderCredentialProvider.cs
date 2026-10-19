using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Infrastructure.Database.Identity;

/// <summary>
/// Credential access over <see cref="IdentityDbContext"/>, the same scoped context
/// <see cref="SingleUserProvider"/> saves through — which is what lets that save commit the changes
/// staged here. Reads track, as that context does by default, so a caller can mutate what it gets.
/// </summary>
public sealed class ProviderCredentialProvider(IdentityDbContext dbContext)
    : IProviderCredentialProvider
{
    public Task<ProviderCredential?> GetAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct = default
    ) =>
        dbContext
            .ProviderCredentials.Where(credential =>
                credential.UserId == userId && credential.Provider == provider
            )
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProviderCredential>> ListAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProviderCredentials.Where(credential => credential.UserId == userId)
            .OrderBy(credential => credential.Provider)
            .ToListAsync(ct);

    public void Add(ProviderCredential credential) => dbContext.ProviderCredentials.Add(credential);

    public void Remove(ProviderCredential credential) =>
        dbContext.ProviderCredentials.Remove(credential);
}
