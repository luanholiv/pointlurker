namespace PointLurker.Core.Application.Stores;

/// <summary>
/// What a secret says about itself, read without a network call. Untrusted hints, never
/// authentication: the provider still decides whether the secret works.
/// </summary>
/// <param name="ExpiresAt">When the secret announces it stops working, if it announces it at all.</param>
/// <param name="AccountName">The account name the secret carries, if any.</param>
public sealed record ProviderCredentialHints(DateTimeOffset? ExpiresAt, string? AccountName)
{
    /// <summary>A secret that says nothing about itself — an opaque token.</summary>
    public static ProviderCredentialHints None { get; } = new(null, null);
}
