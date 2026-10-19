using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Emails the owner when a stored credential's own expiry is near, once per expiry. A provider
/// session that dies silently stops auto-redeem for that provider, so the warning is what gives
/// the owner the chance to paste a new one first.
/// </summary>
/// <remarks>
/// Only a secret that announces its expiry is covered; one that dies early is caught by
/// <see cref="RefreshProviderAccounts"/> instead. A failed send leaves the warning owed, so the next
/// pass inside the window tries again.
/// </remarks>
public sealed class WarnExpiringCredentials(
    ISingleUserProvider userProvider,
    IProviderCredentialProvider credentials,
    IEmailNotifier emailNotifier,
    IOptions<CredentialExpiryOptions> options,
    TimeProvider timeProvider,
    ILogger<WarnExpiringCredentials> logger
)
{
    private readonly TimeSpan _window = TimeSpan.FromHours(options.Value.ExpiryWarningHours);

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
            return;

        var now = timeProvider.GetUtcNow();
        var owed = (await credentials.ListAsync(user.Id, ct))
            .Where(credential => credential.NeedsExpiryWarning(now, _window))
            .ToList();
        if (owed.Count == 0)
            return;

        // Checked here rather than left to the mail server: a bare word parses as a mailbox.
        var address = user.Email ?? user.UserName;
        if (address is null)
        {
            logger.LogCredentialExpiryWarningNoAddress(owed.Count);
            return;
        }

        foreach (var credential in owed)
            await WarnOneAsync(user, address, credential, now, ct);
    }

    /// <remarks>
    /// Committed per credential: the email has already gone out by the time the marker is staged,
    /// and a batched commit that failed would send every one of them again.
    /// </remarks>
    private async Task WarnOneAsync(
        ApplicationUser user,
        string address,
        ProviderCredential credential,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var expiresAt = credential.ExpiresAt!.Value;
        try
        {
            await emailNotifier.SendCredentialExpiryAsync(
                address,
                new CredentialExpiryEmailModel(
                    credential.Provider,
                    expiresAt,
                    HasExpired: credential.IsExpiredAt(now)
                ),
                user.Locale,
                ct
            );
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogCredentialExpiryWarningFailed(credential.Provider, expiresAt, ex);
            return;
        }

        credential.MarkExpiryWarningSent();
        await userProvider.SaveAsync(user, ct);
        logger.LogCredentialExpiryWarningSent(credential.Provider, expiresAt);
    }
}
