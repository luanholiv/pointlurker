using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Re-checks, for one store, whether every auto-redeem wishlist entry still has its item's user
/// inputs covered by the store input profile — blocking the entries that lost coverage, unblocking
/// the ones that regained it, and logging each label's resolution outcome as it changes.
/// <para>
/// Resolved per store from its own scope (<see cref="ScopedInstance{T}"/>), because the stores of a
/// cycle are evaluated in parallel and its repositories are scoped.
/// </para>
/// </summary>
public sealed class StoreInputCoverageUnit(
    IWatchedStoreRepository storeRepo,
    IWishlistEntryRepository wishlistRepo,
    IStoreInputResolutionLogRepository logRepo,
    IInputLabelResolver resolver,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<StoreInputCoverageUnit> logger
)
{
    public async Task<IReadOnlyList<BlockedEvent>> ExecuteAsync(Guid storeId, CancellationToken ct)
    {
        var store = await storeRepo.GetByIdAsync(storeId, ct);
        if (store is null)
        {
            return [];
        }

        var entries = await wishlistRepo.ListByStoreWithItemsForUpdateAsync(storeId, ct);

        var latestOutcomes = await logRepo.GetLatestPerLabelAsync(storeId, ct);
        var logEntries = new List<StoreInputResolutionLogEntry>();
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow();
        foreach (var item in store.Items)
        {
            foreach (var def in item.UserInputDefinitions)
            {
                var label = NormalizeLabel(def);
                if (label.Length == 0 || !seenLabels.Add(label))
                {
                    continue;
                }
                var resolved = resolver.Resolve(label, store);
                var (canonical, score, source) = resolved switch
                {
                    ResolutionResult.Resolved r => (r.Canonical, r.Confidence, r.Source),
                    ResolutionResult.Unresolved u => (
                        (string?)null,
                        u.BestScore,
                        ResolutionSource.Unresolved
                    ),
                    _ => (null, 0, ResolutionSource.Unresolved),
                };

                // Only outcome transitions are logged; confidence-only
                // fluctuations within the same outcome are not.
                if (
                    latestOutcomes.TryGetValue(label, out var latest)
                    && latest.Source == source
                    && string.Equals(
                        latest.CanonicalLabel,
                        canonical,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue;
                }

                logEntries.Add(
                    new StoreInputResolutionLogEntry(
                        Guid.NewGuid(),
                        storeId,
                        label,
                        canonical,
                        score,
                        source,
                        now
                    )
                );
            }
        }

        if (logEntries.Count > 0)
        {
            logRepo.AddRange(logEntries);
        }

        var blockedEvents = new List<BlockedEvent>();

        foreach (var entry in entries)
        {
            var item = entry.StoreItem;
            if (item is null || item.UserInputDefinitions.Count == 0)
            {
                if (entry.IsBlockedOnInput)
                {
                    entry.ClearInputBlock(now);
                    wishlistRepo.Update(entry);
                    logger.LogWishlistEntryInputCoverageRestored(entry.Id);
                }
                continue;
            }

            var missing = InputCoverage.ComputeMissingLabels(
                item.UserInputDefinitions,
                store,
                resolver
            );

            if (missing.Count > 0)
            {
                if (entry.AutoRedeem)
                {
                    entry.BlockOnMissingInput(now);
                    wishlistRepo.Update(entry);
                    logger.LogWishlistEntryBlockedOnInput(entry.Id, string.Join(", ", missing));
                    blockedEvents.Add(
                        new BlockedEvent(
                            entry.Id,
                            entry.StoreItemId,
                            item.Name,
                            store.DisplayName,
                            missing
                        )
                    );
                }
            }
            else if (entry.IsBlockedOnInput)
            {
                entry.ClearInputBlock(now);
                wishlistRepo.Update(entry);
                logger.LogWishlistEntryInputCoverageRestored(entry.Id);
            }
        }

        // One commit for the store's whole re-evaluation — the log rows and every block or unblock
        // together. Committing per entry would let a mid-loop failure leave the store half-evaluated
        // with no record of where it stopped.
        await unitOfWork.SaveChangesAsync(ct);

        return blockedEvents;
    }

    private static string NormalizeLabel(string? label) =>
        string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : string.Join(' ', label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
