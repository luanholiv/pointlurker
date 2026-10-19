using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

public sealed class PollRedemptionStatus(
    IRedemptionRepository redemptionRepo,
    IStoreProviderClientFactory providers,
    ProviderSecrets secrets,
    ISingleUserProvider userProvider,
    IUnitOfWork unitOfWork,
    IOptions<RedemptionsOptions> options,
    TimeProvider timeProvider,
    ILogger<PollRedemptionStatus> logger
)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var submitted = await redemptionRepo.GetSubmittedForUpdateAsync(ct);
        if (submitted.Count == 0)
            return;

        var now = timeProvider.GetUtcNow();
        var timeoutThreshold = TimeSpan.FromHours(options.Value.CompletionTimeoutHours);

        var user = await userProvider.GetAsync(ct);
        var secretsByProvider = new Dictionary<StoreProvider, string?>();

        var staged = false;
        foreach (var redemption in submitted)
        {
            if (TimeOut(redemption, now, timeoutThreshold))
            {
                staged = true;
                continue;
            }

            // Only once the row has not timed out: a provider that cannot be read back, or whose
            // credential is unusable, must not also suspend the timeout above — rows would then sit
            // in Submitted forever, blocking auto-redeem for their wishlist entry.
            var client = providers.For(redemption.Provider);
            if (!client.Capabilities.SupportsRedemptionPolling)
                continue;

            if (!secretsByProvider.TryGetValue(redemption.Provider, out var secret))
            {
                secret = user is null
                    ? null
                    : await TryResolveSecretAsync(user.Id, redemption.Provider, ct);
                secretsByProvider[redemption.Provider] = secret;
            }

            if (secret is not null)
                staged |= await PollOneAsync(client, redemption, secret, ct);
        }

        // One commit for the sweep, and none at all on the common tick where nothing has finished
        // upstream. Polling only mirrors state the provider already holds, so a failure part-way
        // costs nothing: the next tick re-polls whatever did not get written.
        if (staged)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <summary>Times the redemption out when it has waited too long, staging the change.</summary>
    private bool TimeOut(Redemption redemption, DateTimeOffset now, TimeSpan timeoutThreshold)
    {
        if (
            !redemption.SubmittedAt.HasValue
            || now - redemption.SubmittedAt.Value <= timeoutThreshold
        )
            return false;

        logger.LogRedemptionTimedOut(redemption.Id, redemption.SubmittedAt.Value);
        redemption.MarkTimedOut();
        redemptionRepo.Update(redemption);
        return true;
    }

    /// <summary>
    /// Resolves the owner's credential for <paramref name="provider"/>, or <see langword="null"/>
    /// when it is absent or cannot be decrypted. A null answer disables that provider's upstream
    /// read for this cycle, nothing more.
    /// </summary>
    private async Task<string?> TryResolveSecretAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct
    )
    {
        switch (await secrets.ResolveAsync(userId, provider, ct))
        {
            case ProviderSecretResolution.Resolved resolved:
                return resolved.Secret;

            case ProviderSecretResolution.DecryptionFailed failed:
                logger.LogPollDecryptCredentialFailed(provider, failed.Exception);
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Polls one redemption, returning whether it staged a change for the caller to commit.
    /// </summary>
    private async Task<bool> PollOneAsync(
        IStoreProviderClient client,
        Redemption redemption,
        string secret,
        CancellationToken ct
    )
    {
        try
        {
            var response = await client.GetRedemptionAsync(
                redemption.StoreExternalId,
                redemption.ProviderRedemptionId!,
                secret,
                ct
            );

            // A refusal comes back alongside completed:false. Checked first, or it reads as work
            // still pending and waits out the completion timeout under the wrong status.
            if (response.Rejected)
            {
                logger.LogRedemptionRejected(redemption.Id, response.UpdatedAt);
                redemption.MarkRejected(response.UpdatedAt);
                redemptionRepo.Update(redemption);
                return true;
            }

            if (!response.Completed)
            {
                logger.LogRedemptionNotYetCompleted(redemption.Id);

                // The read itself worked, so an earlier failure trail is stale. Staging only when
                // there was something to clear keeps the healthy tick free of writes.
                if (!redemption.ClearPollFailures())
                    return false;

                redemptionRepo.Update(redemption);
                return true;
            }

            redemption.ClearPollFailures();
            redemption.MarkCompleted(response.UpdatedAt);
            redemptionRepo.Update(redemption);
            logger.LogRedemptionCompleted(redemption.Id, response.UpdatedAt);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown, not an upstream fault. Recording it would inflate the failure count.
            throw;
        }
        catch (Exception ex)
        {
            // The status stays Submitted on purpose: the submission succeeded, and only the reading
            // of its outcome failed. Reopening it would let auto-redeem spend the owner's points a
            // second time on an item that may already be redeemed upstream. Count it instead, so a
            // permanently broken check is visible rather than looking like work still pending.
            redemption.RecordPollFailure($"{ex.GetType().Name}: {ex.Message}");
            redemptionRepo.Update(redemption);
            logger.LogPollRedemptionFailed(redemption.Id, redemption.PollFailures, ex);

            if (redemption.PollFailures == options.Value.PollFailureAlertThreshold)
                logger.LogPollRedemptionPersistentlyFailing(redemption.Id, redemption.PollFailures);

            return true;
        }
    }
}
