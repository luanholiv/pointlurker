using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Notifications;

/// <summary>
/// Emails each gift-card code still owed to the owner. The database is the queue: every code whose
/// delivery is due is read afresh on each run, so a signal lost to a restart costs latency, never
/// a code.
/// </summary>
public sealed class DeliverFulfillmentCodes(
    IFulfillmentCodeRepository codes,
    [FromKeyedServices(ProtectionPurposes.FulfillmentCode)] ISecretProtector codeProtector,
    IEmailNotifier emailNotifier,
    INotificationRepository notificationRepo,
    ISingleUserProvider userProvider,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<DeliverFulfillmentCodes> logger
)
{
    /// <summary>The longest wait between attempts, so a code stuck on a broken mail setup is still retried a few times a day.</summary>
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(6);

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
            return;

        var due = await codes.GetDueForUpdateAsync(timeProvider.GetUtcNow(), ct);

        foreach (var delivery in due)
            await DeliverOneAsync(user, delivery, ct);
    }

    /// <remarks>
    /// Committed per code: each wraps an email that has already gone out by this point, and a
    /// batched commit that failed would send every one of them again.
    /// </remarks>
    private async Task DeliverOneAsync(
        ApplicationUser user,
        PendingFulfillmentDelivery delivery,
        CancellationToken ct
    )
    {
        var code = delivery.Code;
        var failure = await TrySendAsync(user, delivery, ct);
        var now = timeProvider.GetUtcNow();

        if (failure is null)
        {
            code.MarkSent(now);
            notificationRepo.Add(AuditRecord(user.Id, delivery, now));
            logger.LogFulfillmentCodeDelivered(code.RedemptionId);
        }
        else
        {
            var nextRetryAt = now + Backoff(code.Attempts);
            code.RecordFailedAttempt(failure, nextRetryAt);
            logger.LogFulfillmentCodeDeliveryFailed(
                code.RedemptionId,
                code.Attempts,
                nextRetryAt,
                failure
            );
        }

        codes.Update(code);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <returns>Null when the email went out, otherwise why it did not — never containing the code.</returns>
    private async Task<string?> TrySendAsync(
        ApplicationUser user,
        PendingFulfillmentDelivery delivery,
        CancellationToken ct
    )
    {
        // Checked here rather than left to the mail server, for the same reason the availability
        // email checks it: a bare word parses as a mailbox.
        var address = user.Email ?? user.UserName;
        if (address is null)
            return "The account has no email address to deliver to.";

        string plaintext;
        try
        {
            plaintext = codeProtector.Unprotect(delivery.Code.EncryptedCode);
        }
        catch (Exception ex)
        {
            // Retried rather than abandoned: a key ring restored from backup makes the code
            // readable again, and the row is the only copy there is.
            logger.LogFulfillmentCodeDecryptionFailed(delivery.Code.RedemptionId, ex);
            return $"The code could not be decrypted ({ex.GetType().Name}).";
        }

        try
        {
            await emailNotifier.SendFulfillmentCodeAsync(
                address,
                new FulfillmentCodeEmailModel(
                    delivery.StoreName,
                    delivery.StoreItemName,
                    plaintext
                ),
                user.Locale,
                ct
            );
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The message of an exception is outside this code's control, so the code is struck
            // from it before it is stored or logged.
            return $"{ex.GetType().Name}: {ex.Message}".Replace(
                plaintext,
                "<redacted>",
                StringComparison.Ordinal
            );
        }
    }

    /// <summary>One minute, doubling per attempt, up to <see cref="MaxBackoff"/>.</summary>
    private static TimeSpan Backoff(int priorAttempts)
    {
        var minutes = Math.Min(Math.Pow(2, priorAttempts), MaxBackoff.TotalMinutes);
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// The owner-facing record that the email went out. The delivery state itself stays on the code
    /// row; this is the history, the same kind of row an availability email leaves.
    /// </summary>
    private static Notification AuditRecord(
        Guid userId,
        PendingFulfillmentDelivery delivery,
        DateTimeOffset sentAt
    )
    {
        var notification = new Notification(Guid.NewGuid(), userId, NotificationChannel.Email);
        notification.AddItem(
            new NotificationItem(
                Guid.NewGuid(),
                notification.Id,
                delivery.StoreItemId,
                delivery.StoreName,
                delivery.StoreItemName
            )
        );
        notification.MarkSent(sentAt);
        return notification;
    }
}
