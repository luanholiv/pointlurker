using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

public sealed class SubmitRedemptions(
    IRedemptionRepository redemptionRepo,
    IStoreItemRepository storeItemRepo,
    IStoreInputValueResolver inputValues,
    IStoreProviderClientFactory providers,
    ProviderSecrets secrets,
    [FromKeyedServices(ProtectionPurposes.RedemptionMessage)] ISecretProtector messageProtector,
    IFulfillmentCodeRepository fulfillmentCodes,
    [FromKeyedServices(ProtectionPurposes.FulfillmentCode)] ISecretProtector codeProtector,
    ChannelWriter<FulfillmentCodeReady> fulfillmentSignal,
    ISingleUserProvider userProvider,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SubmitRedemptions> logger
)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
            return;

        var pending = await redemptionRepo.GetPendingForUpdateAsync(timeProvider.GetUtcNow(), ct);
        if (pending.Count == 0)
            return;

        // One credential per provider, and a missing one fails only that provider's rows: a
        // redemption owes nothing to a provider it is not being submitted to.
        foreach (var batch in pending.GroupBy(redemption => redemption.Provider))
            await SubmitBatchAsync(user.Id, batch.Key, batch.ToArray(), ct);
    }

    private async Task SubmitBatchAsync(
        Guid userId,
        StoreProvider provider,
        IReadOnlyList<Redemption> batch,
        CancellationToken ct
    )
    {
        switch (await secrets.ResolveAsync(userId, provider, ct))
        {
            case ProviderSecretResolution.Resolved resolved:
                foreach (var redemption in batch)
                    await SubmitOneAsync(redemption, resolved.Secret, ct);
                return;

            case ProviderSecretResolution.Expired expired:
                // Known from the credential alone: sending would only spend a call on a refusal.
                await MarkAllFailedAsync(
                    batch,
                    SubmissionFailureCode.CredentialExpired,
                    $"{provider} credential expired at {expired.Credential.ExpiresAt:O}",
                    ct
                );
                return;

            case ProviderSecretResolution.DecryptionFailed failed:
                logger.LogSubmitDecryptCredentialFailed(provider, failed.Exception);
                await MarkAllFailedAsync(
                    batch,
                    SubmissionFailureCode.TokenDecryptionFailed,
                    failed.Exception.Message,
                    ct
                );
                return;

            case ProviderSecretResolution.NotConfigured:
                await MarkAllFailedAsync(
                    batch,
                    SubmissionFailureCode.TokenDecryptionFailed,
                    $"{provider} credential not configured",
                    ct
                );
                return;

            default:
                throw new UnreachableException(
                    "ProviderSecretResolution has no case beyond Resolved, Expired, DecryptionFailed and NotConfigured."
                );
        }
    }

    private async Task SubmitOneAsync(Redemption redemption, string secret, CancellationToken ct)
    {
        var inputs = await ResolveInputsAsync(redemption, ct);
        if (inputs is null)
        {
            redemptionRepo.Update(redemption);
            await unitOfWork.SaveChangesAsync(ct);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var message = redemption.MessageSnapshot is { Length: > 0 } ciphertext
            ? messageProtector.Unprotect(ciphertext)
            : redemption.MessageSnapshot;
        var command = new ProviderRedeemCommand(
            redemption.Id,
            redemption.StoreExternalId,
            redemption.ProviderItemId,
            inputs,
            message
        );

        var codeStaged = false;

        try
        {
            var result = await providers.For(redemption.Provider).RedeemAsync(command, secret, ct);

            switch (result)
            {
                case ProviderRedemptionResult.Accepted accepted:
                    var submittedAt = timeProvider.GetUtcNow();
                    redemption.MarkSubmitted(accepted.RedemptionId, submittedAt);

                    if (accepted.Completed)
                        redemption.MarkCompleted(submittedAt);

                    if (accepted.FulfillmentCode is { Length: > 0 } fulfillmentCode)
                        codeStaged = StageFulfillmentCode(redemption, fulfillmentCode, submittedAt);

                    logger.LogRedemptionSubmitted(
                        redemption.Id,
                        redemption.Provider,
                        accepted.RedemptionId,
                        accepted.Completed
                    );
                    break;

                case ProviderRedemptionResult.InsufficientPoints refused:
                    // The provider's own words, classified by its client. Diagnostic only and never
                    // returned to clients.
                    logger.LogRedemptionInsufficientPoints(redemption.Id, refused.Detail);
                    redemption.MarkFailed(SubmissionFailureCode.InsufficientPoints, refused.Detail);
                    break;

                case ProviderRedemptionResult.Refused refused:
                    logger.LogRedemptionRefused(redemption.Id, refused.Detail);
                    redemption.MarkFailed(SubmissionFailureCode.ProviderRejected, refused.Detail);
                    break;

                default:
                    throw new UnreachableException(
                        "ProviderRedemptionResult has no case beyond Accepted, InsufficientPoints and Refused."
                    );
            }
        }
        catch (ProviderApiException ex) when (ex.StatusCode is 400 or 401 or 403 or 404 or >= 500)
        {
            var context = $"HTTP {ex.StatusCode}: {ex.ReasonPhrase ?? ex.Message}";
            var code = ex.StatusCode switch
            {
                400 => SubmissionFailureCode.ProviderApiBadRequest,
                401 => SubmissionFailureCode.ProviderApiUnauthorized,
                403 => SubmissionFailureCode.ProviderApiForbidden,
                404 => SubmissionFailureCode.ProviderApiNotFound,
                _ => SubmissionFailureCode.ProviderApiServerError,
            };
            logger.LogRedemptionPermanentFailure(redemption.Id, ex.StatusCode, context);
            redemption.MarkFailed(code, context);
        }
        catch (Exception ex)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, redemption.Attempts + 1));
            var jitter = TimeSpan.FromSeconds(Random.Shared.NextDouble());
            var nextRetryAt = now + delay + jitter;
            logger.LogRedemptionScheduledRetry(redemption.Id, nextRetryAt, ex);
            redemption.ScheduleRetry(nextRetryAt);
        }

        // Committed per redemption, deliberately: each one wraps a provider call that has already
        // happened by this point, and its outcome has to be durable before the next submission runs.
        // Batching the loop would risk re-submitting redemptions that succeeded upstream.
        redemptionRepo.Update(redemption);
        await unitOfWork.SaveChangesAsync(ct);

        // Commit first, signal second: a signal for a row that never landed would send the delivery
        // looking for nothing, and a commit that threw has already left this method.
        if (codeStaged)
            fulfillmentSignal.TryWrite(FulfillmentCodeReady.Instance);
    }

    /// <summary>
    /// Stages the code in the same unit of work as the redemption's own outcome, so both commit
    /// together: committed apart, a crash between the two would spend the points and lose the code.
    /// </summary>
    /// <returns>Whether a row was staged.</returns>
    /// <remarks>
    /// A failure here is contained rather than thrown. The provider has already spent the points, so
    /// letting it reach the retry path would submit the redemption a second time — the one outcome
    /// worse than losing the code.
    /// </remarks>
    private bool StageFulfillmentCode(
        Redemption redemption,
        string fulfillmentCode,
        DateTimeOffset createdAt
    )
    {
        try
        {
            fulfillmentCodes.Add(
                new FulfillmentCode(
                    Guid.NewGuid(),
                    redemption.Id,
                    codeProtector.Protect(fulfillmentCode),
                    createdAt
                )
            );
            return true;
        }
        catch (Exception ex)
        {
            logger.LogFulfillmentCodeNotStored(redemption.Id, ex);
            return false;
        }
    }

    private async Task<IReadOnlyList<ProviderInputValue>?> ResolveInputsAsync(
        Redemption redemption,
        CancellationToken ct
    )
    {
        var item = await storeItemRepo.GetByIdWithStoreAsync(redemption.StoreItemId, ct);
        if (item is null || item.WatchedStore is null)
        {
            redemption.MarkFailed(SubmissionFailureCode.InputCoverageLost, "<store missing>");
            logger.LogRedemptionInputCoverageLost(redemption.Id, "<store missing>");
            return null;
        }

        switch (inputValues.Resolve(item, item.WatchedStore))
        {
            case StoreInputResolution.Resolved resolved:
                return resolved.Values;

            case StoreInputResolution.DecryptionFailed failed:
                logger.LogRedemptionInputDecryptionFailed(redemption.Id, failed.Exception);
                redemption.MarkFailed(SubmissionFailureCode.InputDecryptionFailed);
                return null;

            case StoreInputResolution.Missing missing:
                var joined = string.Join(", ", missing.Labels);
                redemption.MarkFailed(SubmissionFailureCode.InputCoverageLost, joined);
                logger.LogRedemptionInputCoverageLost(redemption.Id, joined);
                return null;

            default:
                throw new UnreachableException(
                    "StoreInputResolution has no case beyond Resolved, Missing and DecryptionFailed."
                );
        }
    }

    /// <summary>
    /// Fails one provider's batch for a reason that applies to all of it — no credential, or one that
    /// will not decrypt. One commit: the cause is shared, so the outcome should be too.
    /// </summary>
    private async Task MarkAllFailedAsync(
        IReadOnlyList<Redemption> redemptions,
        SubmissionFailureCode code,
        string? context,
        CancellationToken ct
    )
    {
        foreach (var r in redemptions)
        {
            r.MarkFailed(code, context);
            redemptionRepo.Update(r);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
