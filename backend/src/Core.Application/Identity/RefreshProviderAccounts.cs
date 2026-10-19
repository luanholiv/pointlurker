using Microsoft.Extensions.Logging;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Re-checks every stored provider credential and refreshes the account name it authenticates as.
/// Idempotent and failure-tolerant: one provider failing does not stop the others, and a failed
/// lookup never clears a known account name. A provider refusing the credential is recorded on it,
/// which is how a dead credential becomes visible before a redemption trips over it.
/// </summary>
public sealed class RefreshProviderAccounts(
    ISingleUserProvider userProvider,
    IProviderCredentialProvider credentials,
    ProviderSecretProtectors protectors,
    IStoreProviderClientFactory providers,
    TimeProvider timeProvider,
    ILogger<RefreshProviderAccounts> logger
)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
            return;

        var held = await credentials.ListAsync(user.Id, ct);
        var staged = false;

        foreach (var credential in held)
            staged |= await RefreshOneAsync(credential, ct);

        if (staged)
            await userProvider.SaveAsync(user, ct);
    }

    /// <summary>Refreshes one credential, returning whether it staged a change.</summary>
    private async Task<bool> RefreshOneAsync(ProviderCredential credential, CancellationToken ct)
    {
        var provider = credential.Provider;
        var protector = protectors.For(provider);
        if (protector is null)
        {
            logger.LogAccountRefreshUnsupportedProvider(provider);
            return false;
        }

        string secret;
        try
        {
            secret = protector.Unprotect(credential.EncryptedSecret);
        }
        catch (Exception ex)
        {
            logger.LogAccountRefreshDecryptFailed(provider, ex);
            return false;
        }

        try
        {
            var accountName = await providers.For(provider).GetAccountNameAsync(secret, ct);
            var now = timeProvider.GetUtcNow();

            credential.MarkValidated(now);
            if (!string.IsNullOrWhiteSpace(accountName))
                credential.SetAccountName(accountName, now);

            logger.LogAccountRefreshed(provider, accountName);
            return true;
        }
        catch (ProviderApiException ex) when (SetProviderCredential.IsRefusal(ex))
        {
            credential.MarkValidationFailed(ErrorCodes.Credentials.Rejected);
            logger.LogAccountRefreshRejected(provider, ex.StatusCode);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogAccountRefreshFailed(provider, ex);
            return false;
        }
    }
}
