using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Sequences one sync cycle: every active store is fetched and diffed in parallel, then the
/// cycle-wide phases run once — prune the resolution log, auto-redeem, notify.
/// <para>
/// The cycle-wide phases run in this instance's own scope, opened by the hosted service that starts
/// the cycle. Only the per-store pass needs more than that: it is parallel, so each store gets a
/// fresh <see cref="StoreSyncUnit"/> — and with it a fresh <c>DbContext</c> — from
/// <c>Func&lt;ScopedInstance&lt;StoreSyncUnit&gt;&gt;</c>.
/// </para>
/// </summary>
public sealed class SyncStores(
    ISingleUserProvider userProvider,
    ProviderSecrets secrets,
    IWatchedStoreRepository storeRepo,
    IStoreItemRepository itemRepo,
    IWishlistEntryRepository wishlistRepo,
    IRedemptionRepository redemptionRepo,
    INotificationRepository notificationRepo,
    IStoreInputResolutionLogRepository logRepo,
    IStoreProviderClientFactory providers,
    IEmailNotifier emailNotifier,
    ChannelWriter<Redemption> redemptionChannel,
    IStoreInputValueResolver inputValues,
    Func<ScopedInstance<StoreSyncUnit>> storeSyncUnit,
    IUnitOfWork unitOfWork,
    IOptions<SyncSettings> settings,
    TimeProvider timeProvider,
    ILogger<SyncStores> logger
)
{
    public async Task<SyncResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var user = await userProvider.GetAsync(cancellationToken);

        if (user is null)
        {
            logger.LogNoUserFound();
            return new SyncResult(0, 0, 0, 0, 0, 0, 0);
        }

        // No credential gate here: reading a store's catalogue needs none, and a missing
        // credential for one provider must not stop another provider's stores from syncing. Only
        // auto-redeem needs one, and it asks per provider.
        var active = await storeRepo.ListActiveAsync(cancellationToken);

        if (active.Count == 0)
        {
            logger.LogNoActiveStores();
            return new SyncResult(0, 0, 0, 0, 0, 0, 0);
        }

        var cycleStartedAt = timeProvider.GetUtcNow();
        var stores = active.Where(store => IsDue(store, cycleStartedAt)).ToArray();
        var storesSkipped = active.Count - stores.Length;

        // Phase 1 — Fetch + Check
        var outcomes = new ConcurrentBag<StoreSyncOutcome>();
        var storesSynced = 0;

        await Parallel.ForEachAsync(
            stores,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = settings.Value.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            },
            async (store, ct) =>
            {
                try
                {
                    await using var unit = storeSyncUnit();
                    outcomes.Add(await unit.Value.ExecuteAsync(store, ct));
                    Interlocked.Increment(ref storesSynced);
                }
                catch (Exception ex)
                {
                    logger.LogSyncStoreFailed(store.Id, store.DisplayName, ex);
                }
            }
        );

        var itemsProcessed = outcomes.Sum(outcome => outcome.ItemsProcessed);
        var itemsChanged = outcomes.Sum(outcome => outcome.ItemsChanged);
        var availableEvents = outcomes.SelectMany(outcome => outcome.AvailabilityEvents).ToArray();
        var blockedEvents = outcomes.SelectMany(outcome => outcome.BlockedEvents).ToArray();

        await PruneResolutionLogAsync(cancellationToken);

        // Phase 2 — Auto-Redeem (creates Pending redemptions, posts to Channel)
        var (redemptionsAttempted, redemptionsPending, redeemedItemIds) = await AutoRedeemAsync(
            user,
            cancellationToken
        );

        // Phase 3 — Notify
        var notificationsSent = await NotifyAsync(
            user,
            availableEvents,
            redeemedItemIds,
            cancellationToken
        );

        await PersistBlockedNotificationsAsync(user.Id, blockedEvents, cancellationToken);

        return new SyncResult(
            storesSynced,
            storesSkipped,
            itemsProcessed,
            itemsChanged,
            redemptionsAttempted,
            redemptionsPending,
            notificationsSent
        );
    }

    /// <summary>
    /// Whether the store's provider allows another fetch yet. A store never synced is always due,
    /// and a zero interval makes every tick due.
    /// </summary>
    private bool IsDue(WatchedStore store, DateTimeOffset now)
    {
        var interval = providers.For(store.Provider).Capabilities.MinimumSyncInterval;
        return interval <= TimeSpan.Zero
            || store.LastSyncedAt is null
            || store.LastSyncedAt.Value <= now - interval;
    }

    private async Task PruneResolutionLogAsync(CancellationToken ct)
    {
        try
        {
            var cutoff = timeProvider
                .GetUtcNow()
                .AddDays(-settings.Value.ResolutionLogRetentionDays);
            var deleted = await logRepo.PruneAsync(cutoff, ct);
            if (deleted > 0)
            {
                logger.LogResolutionLogPruned(deleted, cutoff);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogResolutionLogPruneFailed(ex);
        }
    }

    private async Task<(int Attempted, int Pending, HashSet<Guid> RedeemedItemIds)> AutoRedeemAsync(
        ApplicationUser user,
        CancellationToken ct
    )
    {
        var entries = await wishlistRepo.ListAutoRedeemWithItemsForUpdateAsync(user.Id, ct);

        // One round trip for the whole pass, before the loop: quota and inflight come off the same
        // scan, and asking per entry would put a round trip inside it.
        var quotaUsage = await redemptionRepo.CountHoldingQuotaAsync(
            entries.Select(entry => entry.Id).ToArray(),
            ct
        );

        var minimumInterval = TimeSpan.FromMinutes(settings.Value.MinimumAutoRedeemIntervalMinutes);

        var attempted = 0;
        var redeemedItemIds = new HashSet<Guid>();

        // Staged here, published after the commit below.
        var created = new List<Redemption>();

        // Per-cycle state: each provider's account resolved at most once, balance fetched at most
        // once per store, running spend prevents overspend. Keyed by provider as well as store id:
        // an external id is only unique within its provider.
        var accountByProvider = new Dictionary<StoreProvider, ProviderAccountAccess?>();
        var balanceByStore = new Dictionary<(StoreProvider, StoreExternalId), long?>();
        var spentByStore = new Dictionary<(StoreProvider, StoreExternalId), long>();

        foreach (var entry in entries)
        {
            var item = entry.StoreItem;
            if (item is null)
            {
                continue;
            }

            if (!accountByProvider.TryGetValue(item.Provider, out var account))
            {
                account = await ResolveAccountAsync(user.Id, item.Provider, ct);
                accountByProvider[item.Provider] = account;
            }

            // Fail-safe: without a usable account the balance cannot be verified, so this
            // provider's entries are skipped for the cycle. Other providers carry on.
            if (account is null)
            {
                continue;
            }

            var storeKey = (item.Provider, item.StoreExternalId);
            if (!balanceByStore.TryGetValue(storeKey, out var balance))
            {
                try
                {
                    balance = await providers
                        .For(item.Provider)
                        .GetPointsAsync(
                            item.StoreExternalId,
                            account.AccountName,
                            account.Secret,
                            ct
                        );
                }
                catch (Exception ex)
                {
                    logger.LogAutoRedeemBalanceFetchFailed(item.StoreExternalId.Value, ex);
                    balance = null;
                }
                balanceByStore[storeKey] = balance;
            }

            // Fail-safe: skip the whole channel this cycle when its balance is unverifiable.
            if (balance is null)
            {
                continue;
            }

            var available = balance.Value - spentByStore.GetValueOrDefault(storeKey);

            var usage = quotaUsage.GetValueOrDefault(entry.Id);
            var policy = new RedemptionPolicy(usage.QuotaUsed, minimumInterval);

            var now = timeProvider.GetUtcNow();
            if (!entry.CanCreateRedemption(item, now, available, policy))
            {
                if (!entry.HasQuotaRemaining(usage.QuotaUsed))
                {
                    logger.LogAutoRedeemSkippedQuotaReached(
                        entry.Id,
                        usage.QuotaUsed,
                        entry.DesiredRedemptions ?? 0
                    );
                }
                else if (entry.AutoRedeem && item.Availability.IsAvailable)
                {
                    if (item.Cost > available)
                    {
                        logger.LogAutoRedeemSkippedInsufficientPoints(
                            entry.Id,
                            item.Cost,
                            available
                        );
                    }
                    else
                    {
                        logger.LogAutoRedeemSkippedCooldown(entry.Id);
                    }
                }
                continue;
            }

            if (usage.Inflight > 0)
            {
                logger.LogAutoRedeemSkippedInflight(entry.Id);
                continue;
            }

            // Gate only: the values are resolved again at submission, from the store profile as it
            // stands then. Anything short of a full resolve means this entry cannot be redeemed.
            var store = item.WatchedStore;
            if (
                store is null
                || inputValues.Resolve(item, store) is not StoreInputResolution.Resolved
            )
            {
                logger.LogAutoRedeemSkippedInputFailure(entry.Id);
                continue;
            }

            attempted++;
            logger.LogAutoRedeemAttempted(entry.Id, item.Id);

            var sentMessage = item.AllowMessages ? (entry.RedemptionMessage ?? "") : null;
            var storeName = store.DisplayName;
            var redemption = entry.CreateRedemption(item, sentMessage, item.Name, storeName, now);

            redemptionRepo.Add(redemption);
            wishlistRepo.Update(entry);
            created.Add(redemption);

            // Reserve the spend so other items in this channel see a reduced balance this cycle.
            spentByStore[storeKey] = spentByStore.GetValueOrDefault(storeKey) + item.Cost;
            redeemedItemIds.Add(item.Id);
        }

        if (created.Count == 0)
        {
            return (attempted, 0, redeemedItemIds);
        }

        // Commit before publishing, and publish nothing if the commit throws. The two failure modes
        // this closes are a redemption queued for a row that never landed, and a row that landed but
        // was never queued — the latter only resolving on the submission service's next timer tick.
        await unitOfWork.SaveChangesAsync(ct);

        foreach (var redemption in created)
        {
            await redemptionChannel.WriteAsync(redemption, ct);
            logger.LogAutoRedeemCreatedPending(
                redemption.Id,
                redemption.WishlistEntryId,
                redemption.StoreItemId
            );
        }

        return (attempted, created.Count, redeemedItemIds);
    }

    /// <summary>
    /// The owner's account for <paramref name="provider"/>, or <see langword="null"/> when it cannot
    /// be used to verify a balance: no credential, no account name recorded for it yet, or a
    /// secret that will not decrypt. Each is logged once per cycle, by the caller's cache.
    /// </summary>
    private async Task<ProviderAccountAccess?> ResolveAccountAsync(
        Guid userId,
        StoreProvider provider,
        CancellationToken ct
    )
    {
        switch (await secrets.ResolveAsync(userId, provider, ct))
        {
            case ProviderSecretResolution.Resolved
            {
                Credential.AccountName: { Length: > 0 } name
            } resolved:
                return new ProviderAccountAccess(name, resolved.Secret);

            case ProviderSecretResolution.Resolved:
                logger.LogAutoRedeemNoAccountName(provider);
                return null;

            case ProviderSecretResolution.Expired:
                logger.LogAutoRedeemCredentialExpired(provider);
                return null;

            case ProviderSecretResolution.DecryptionFailed failed:
                logger.LogAutoRedeemCredentialDecryptFailed(provider, failed.Exception);
                return null;

            default:
                logger.LogAutoRedeemNoCredential(provider);
                return null;
        }
    }

    private async Task<int> NotifyAsync(
        ApplicationUser user,
        IReadOnlyList<AvailabilityEvent> availableEvents,
        HashSet<Guid> redeemedItemIds,
        CancellationToken ct
    )
    {
        if (availableEvents.Count == 0)
        {
            return 0;
        }

        var notifiableEvents = new List<(AvailabilityEvent Event, AvailabilityEmailItem Item)>();
        foreach (var evt in availableEvents)
        {
            var entry = await wishlistRepo.GetByStoreItemAsync(user.Id, evt.StoreItemId, ct);
            if (entry is { NotifyOnAvailable: true })
            {
                var item = await itemRepo.GetByIdAsync(evt.StoreItemId, ct);

                // The row is read for its cost either way, so the rest of what the email shows
                // comes off the same entity rather than a second round trip. A null item means the
                // row went away between the availability event and here; the notification is still
                // worth sending, just without the detail.
                notifiableEvents.Add(
                    (
                        evt,
                        new AvailabilityEmailItem(
                            evt.ItemName,
                            item?.Cost ?? 0,
                            redeemedItemIds.Contains(evt.StoreItemId),
                            item?.CategoryName ?? string.Empty,
                            item?.SubscriberOnly ?? false,
                            item?.Quantity.Current ?? -1,
                            item?.Quantity.Total ?? -1
                        )
                    )
                );
            }
        }

        if (notifiableEvents.Count == 0)
        {
            return 0;
        }

        var storeGroups = notifiableEvents
            .GroupBy(e => (e.Event.StoreName, e.Event.WatchedStoreId))
            .Select(g => new AvailabilityEmailStoreGroup(
                g.Key.StoreName,
                g.Key.WatchedStoreId,
                g.Select(e => e.Item).ToArray()
            ))
            .ToArray();

        var emailModel = new AvailabilityEmailModel(storeGroups);

        var notification = new Notification(Guid.NewGuid(), user.Id, NotificationChannel.Email);

        // Checked here rather than left to the mail server: a bare word parses as a mailbox, so a
        // placeholder would be built and dispatched and the outcome would depend on how strict the
        // receiving server is. An account with neither address is broken, not a delivery failure.
        var email = user.Email ?? user.UserName;
        if (email is null)
        {
            logger.LogNotificationRecipientMissing();
            notification.MarkFailed("The account has no email address to notify.");
        }
        else
        {
            try
            {
                await emailNotifier.SendAvailabilityAsync(email, emailModel, user.Locale, ct);
                notification.MarkSent(timeProvider.GetUtcNow());
            }
            catch (Exception ex)
            {
                logger.LogSendNotificationFailed(ex);
                notification.MarkFailed(ex.Message);
            }
        }

        foreach (var (evt, _) in notifiableEvents)
        {
            notification.AddItem(
                new NotificationItem(
                    Guid.NewGuid(),
                    notification.Id,
                    evt.StoreItemId,
                    evt.StoreName,
                    evt.ItemName
                )
            );
        }

        notificationRepo.Add(notification);
        await unitOfWork.SaveChangesAsync(ct);

        return notification.Status == NotificationStatus.Sent ? 1 : 0;
    }

    private async Task PersistBlockedNotificationsAsync(
        Guid userId,
        IReadOnlyList<BlockedEvent> blockedEvents,
        CancellationToken ct
    )
    {
        if (blockedEvents.Count == 0)
        {
            return;
        }

        var notification = new Notification(Guid.NewGuid(), userId, NotificationChannel.WebUi);
        foreach (var evt in blockedEvents)
        {
            notification.AddItem(
                new NotificationItem(
                    Guid.NewGuid(),
                    notification.Id,
                    evt.StoreItemId,
                    evt.StoreName,
                    evt.ItemName
                )
            );
        }
        notification.MarkSent(timeProvider.GetUtcNow());
        notificationRepo.Add(notification);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
