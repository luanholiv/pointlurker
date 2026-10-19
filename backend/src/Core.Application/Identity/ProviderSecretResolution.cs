using PointLurker.Core.Domain.Identity;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Outcome of reading one provider's credential for use. Each caller decides what a missing or
/// unreadable credential costs it; none of them lets it stop work for another provider.
/// </summary>
public abstract record ProviderSecretResolution
{
    private ProviderSecretResolution() { }

    /// <summary>The owner holds no credential for the provider, or the provider cannot hold one.</summary>
    public sealed record NotConfigured : ProviderSecretResolution;

    /// <summary>
    /// The credential's own expiry has passed. Not decrypted: nothing sent with it could succeed.
    /// </summary>
    public sealed record Expired(ProviderCredential Credential) : ProviderSecretResolution;

    /// <summary>A credential is stored but its ciphertext would not decrypt.</summary>
    public sealed record DecryptionFailed(Exception Exception) : ProviderSecretResolution;

    /// <param name="Secret">The decrypted secret. A live credential: never logged, never returned.</param>
    public sealed record Resolved(ProviderCredential Credential, string Secret)
        : ProviderSecretResolution;
}
