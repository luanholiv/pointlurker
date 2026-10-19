namespace PointLurker.Core.Application.Stores;

/// <summary>What auto-redeem needs to read the owner's balance in one provider's stores.</summary>
/// <param name="AccountName">The name the credential's owner is known by upstream.</param>
/// <param name="Secret">The decrypted credential. A live secret: never logged.</param>
internal sealed record ProviderAccountAccess(string AccountName, string Secret);
