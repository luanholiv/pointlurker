using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Reads a provider's stored credential and decrypts it under that provider's own purpose. The
/// one path from a <see cref="StoreProvider"/> to a usable secret, so no caller hard-wires the
/// StreamElements protector onto work that belongs to another provider.
/// </summary>
public sealed class ProviderSecrets(
    IProviderCredentialProvider credentials,
    ProviderSecretProtectors protectors,
    TimeProvider timeProvider
)
{
    public async Task<ProviderSecretResolution> ResolveAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct = default
    )
    {
        var protector = protectors.For(provider);
        if (protector is null)
            return new ProviderSecretResolution.NotConfigured();

        var credential = await credentials.GetAsync(userId, provider, ct);
        if (credential is null)
            return new ProviderSecretResolution.NotConfigured();

        if (credential.IsExpiredAt(timeProvider.GetUtcNow()))
            return new ProviderSecretResolution.Expired(credential);

        try
        {
            return new ProviderSecretResolution.Resolved(
                credential,
                protector.Unprotect(credential.EncryptedSecret)
            );
        }
        catch (Exception ex)
        {
            return new ProviderSecretResolution.DecryptionFailed(ex);
        }
    }
}
