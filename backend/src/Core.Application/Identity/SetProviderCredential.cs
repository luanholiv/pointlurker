using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Stores or replaces the owner's credential for one provider, checking it with that provider
/// first. A credential the provider refuses is never stored. One the check cannot reach a verdict
/// on — the provider unreachable, or failing for a reason of its own — is stored unconfirmed, and
/// the account refresh that this triggers tries again.
/// </summary>
public sealed class SetProviderCredential(
    ISingleUserProvider userProvider,
    IProviderCredentialProvider credentials,
    ProviderSecretProtectors protectors,
    IStoreProviderClientFactory providers,
    ChannelWriter<UsernameRefreshSignal> usernameRefreshSignal,
    TimeProvider timeProvider,
    ILogger<SetProviderCredential> logger
)
{
    public async Task<Result<Unit, IdentityError>> ExecuteAsync(
        StoreProvider provider,
        string plainSecret,
        CancellationToken ct = default
    )
    {
        var protector = protectors.For(provider);
        if (protector is null)
        {
            return Result<Unit, IdentityError>.Failure(
                IdentityError.CredentialProviderUnsupported(provider.ToSlug())
            );
        }

        var user = await userProvider.GetAsync(ct);
        if (user is null)
        {
            return Result<Unit, IdentityError>.Failure(IdentityError.UserNotFound());
        }

        var client = providers.For(provider);
        var now = timeProvider.GetUtcNow();

        // Read, never verified: the hints are the secret's own claims. They can only make the
        // check stricter — an expiry already passed is refused without asking the provider.
        var hints = client.DescribeCredential(plainSecret);
        if (hints.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return Result<Unit, IdentityError>.Failure(IdentityError.CredentialExpired(provider));
        }

        string? accountName = null;
        var confirmed = false;
        try
        {
            accountName = await client.GetAccountNameAsync(plainSecret, ct);
            confirmed = true;
        }
        catch (ProviderApiException ex) when (IsRefusal(ex))
        {
            // The status only: a provider's response body can echo what it was sent.
            logger.LogCredentialRejected(provider, ex.StatusCode);
            return Result<Unit, IdentityError>.Failure(IdentityError.CredentialRejected(provider));
        }
        catch (ProviderApiException ex)
        {
            logger.LogCredentialUnconfirmed(provider, ex.StatusCode);
        }

        var encrypted = protector.Protect(plainSecret);

        var credential = await credentials.GetAsync(user.Id, provider, ct);
        if (credential is null)
        {
            credential = new ProviderCredential(user.Id, provider, encrypted, now);
            credentials.Add(credential);
        }
        else
        {
            credential.ReplaceSecret(encrypted, now);
        }

        credential.SetExpiresAt(hints.ExpiresAt);

        // The provider's answer wins; the secret's own claim only fills in when the check could
        // not reach a verdict.
        if (string.IsNullOrWhiteSpace(accountName))
        {
            accountName = hints.AccountName;
        }

        var hasAccountName = !string.IsNullOrWhiteSpace(accountName);
        if (confirmed)
        {
            credential.MarkValidated(now);
        }

        if (hasAccountName)
        {
            credential.SetAccountName(accountName!, now);
        }

        await userProvider.SaveAsync(user, ct);

        // The refresh pass also refreshes the watched stores' channel info, and it is what confirms
        // a credential the check above could not.
        usernameRefreshSignal.TryWrite(UsernameRefreshSignal.Instance);

        return Result<Unit, IdentityError>.Success(Unit.Value);
    }

    /// <summary>
    /// A provider answering 401 or 403 has looked at the credential and said no. Anything else —
    /// no answer, a 5xx, a rate limit — says nothing about the credential.
    /// </summary>
    internal static bool IsRefusal(ProviderApiException ex) => ex.StatusCode is 401 or 403;
}
