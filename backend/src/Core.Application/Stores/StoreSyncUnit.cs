using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One store's slice of a sync cycle: fetch its provider's catalogue, diff it against what is
/// persisted, upsert the result, re-evaluate auto-redeem input coverage, then checkpoint the store.
/// <para>
/// Stores are synced in parallel and everything this depends on is scoped, so the coordinator
/// resolves one unit per store from its own scope (<see cref="ScopedInstance{T}"/>) — never one unit
/// shared across threads.
/// </para>
/// </summary>
public sealed class StoreSyncUnit(
    IStoreItemRepository itemRepo,
    IWatchedStoreRepository storeRepo,
    IStoreProviderClientFactory providers,
    Func<ScopedInstance<StoreInputCoverageUnit>> coverageUnit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
)
{
    public async Task<StoreSyncOutcome> ExecuteAsync(WatchedStore store, CancellationToken ct)
    {
        var apiItems = await providers.For(store.Provider).GetStoreItemsAsync(store.ExternalId, ct);
        var localItems = await itemRepo.ListByStoreForUpdateAsync(store.Id, ct);

        // One observation instant for this store's payload: it stamps LastFetchedAt on every mapped
        // item and therefore dates the availability-transition events the entities raise.
        var fetchedAt = timeProvider.GetUtcNow();

        var apiItemsById = apiItems.ToDictionary(i => i.Id.Value);
        var localItemsById = localItems.ToDictionary(i => i.ProviderItemId.Value);
        var itemsToUpsert = new List<StoreItem>();
        var availabilityEvents = new List<AvailabilityEvent>();

        // Counted apart from the loop's own trip count: every tick re-reads the whole catalogue, so
        // the number of items walked says nothing about whether the store moved. Only the entity can
        // tell — it is the one holding the state the payload is being applied over.
        var changed = 0;

        foreach (var apiItem in apiItems)
        {
            if (localItemsById.TryGetValue(apiItem.Id.Value, out var localItem))
            {
                var change = localItem.UpdateFromApi(apiItem.ToApiData(fetchedAt));
                if (change is StoreItemChange.Changed)
                {
                    changed++;
                }

                HarvestEvents(localItem, store.DisplayName, availabilityEvents);
                itemsToUpsert.Add(localItem);
            }
            else
            {
                var newItem = new StoreItem(Guid.NewGuid(), store.Id, apiItem.ToApiData(fetchedAt));
                HarvestEvents(newItem, store.DisplayName, availabilityEvents);
                itemsToUpsert.Add(newItem);
                changed++;
            }
        }

        foreach (var localItem in localItems)
        {
            if (!apiItemsById.ContainsKey(localItem.ProviderItemId.Value))
            {
                if (localItem.MarkUnavailable(fetchedAt) is StoreItemChange.Changed)
                {
                    changed++;
                }

                HarvestEvents(localItem, store.DisplayName, availabilityEvents);
                itemsToUpsert.Add(localItem);
            }
        }

        itemRepo.Upsert(itemsToUpsert);

        // Committed here rather than folded into the checkpoint below: coverage evaluation reads these
        // items back through its own DbContext, and a query never sees another context's staged rows.
        await unitOfWork.SaveChangesAsync(ct);

        // Coverage runs in its own scope: it writes wishlist entries whose object graphs reach the
        // very items upserted above, and one change tracker cannot hold two instances of the same
        // row. Separate scopes, separate DbContexts, no collision.
        await using var coverage = coverageUnit();
        var blockedEvents = await coverage.Value.ExecuteAsync(store.Id, ct);

        await CheckpointAsync(store.Id, ct);

        return new StoreSyncOutcome(
            itemsToUpsert.Count,
            changed,
            availabilityEvents,
            blockedEvents
        );
    }

    /// <summary>
    /// Records the pass on the store row. The row is re-read here rather than reusing the instance
    /// the cycle started from, so a profile edit made while the cycle was running is not written
    /// back over.
    /// <para>
    /// The channel display name and avatar are deliberately not refreshed here: that is cosmetic
    /// data which changes approximately never, and refreshing it per tick cost one extra
    /// StreamElements call per store per minute. <see cref="RefreshStoreChannelInfo"/> owns it on
    /// the daily refresh schedule instead.
    /// </para>
    /// </summary>
    private async Task CheckpointAsync(Guid storeId, CancellationToken ct)
    {
        var trackedStore = await storeRepo.GetForUpdateAsync(storeId, ct);

        trackedStore!.RecordSync(timeProvider.GetUtcNow());
        storeRepo.Update(trackedStore);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void HarvestEvents(
        StoreItem item,
        string storeName,
        List<AvailabilityEvent> availabilityEvents
    )
    {
        foreach (var domainEvent in item.DomainEvents)
        {
            if (domainEvent is StoreItemBecameAvailable available)
            {
                availabilityEvents.Add(
                    new AvailabilityEvent(
                        available.StoreItemId,
                        available.WatchedStoreId,
                        available.ItemName,
                        storeName
                    )
                );
            }
        }

        item.ClearDomainEvents();
    }
}
