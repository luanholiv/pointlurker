using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class ListWishlistEntriesGrouped(
    IWishlistEntryRepository wishlistEntryRepository,
    IRedemptionRepository redemptionRepository,
    IInputLabelResolver inputLabelResolver
)
{
    public async Task<Result<WishlistGroupedResponse, WishlistError>> ExecuteAsync(
        Guid userId,
        WishlistEntryFilter filter,
        CancellationToken ct = default
    )
    {
        // One query for entry → item → store, narrowed and ordered by the database. The grouping
        // below only walks what came back: entries arrive ordered by store name first, so grouping
        // is a partition of an already-ordered sequence rather than a second sort.
        var entries = await wishlistEntryRepository.ListByUserWithItemsAsync(userId, filter, ct);

        // One batched count for the whole page, so the quota column costs a single query rather
        // than one per row.
        var quotaUsage = await redemptionRepository.CountHoldingQuotaAsync(
            entries.Select(entry => entry.Id).ToArray(),
            ct
        );

        var grouped = entries
            .Where(entry => entry.StoreItem?.WatchedStore is not null)
            // Grouped by the foreign key, not by the store instance: this read does not track, so EF
            // performs no identity resolution and two entries in the same store can arrive holding
            // separate WatchedStore instances. Grouping by reference would split the store in two.
            .GroupBy(entry => entry.StoreItem!.WatchedStoreId)
            .Select(group =>
            {
                var store = group.First().StoreItem!.WatchedStore!;
                var storeEntries = group
                    .Select(entry =>
                    {
                        var item = entry.StoreItem!;

                        // Missing labels stay a computed projection: they are a display field, not a
                        // predicate, so this runs only for the entries actually being returned.
                        IReadOnlyList<string> missing = Array.Empty<string>();
                        if (item.UserInputDefinitions.Count > 0)
                        {
                            missing = InputCoverage.ComputeMissingLabels(
                                item.UserInputDefinitions,
                                store,
                                inputLabelResolver
                            );
                        }

                        return new WishlistEntryGroupItem(
                            entry.Id,
                            entry.StoreItemId,
                            item.Name,
                            item.Cost,
                            item.Availability.IsAvailable,
                            entry.NotifyOnAvailable,
                            entry.AutoRedeem,
                            entry.DesiredRedemptions,
                            quotaUsage.GetValueOrDefault(entry.Id).QuotaUsed,
                            // The persisted flag, not a per-read recompute. Filtering by blocked
                            // state has to happen in SQL, and SQL can only filter on what is stored
                            // — so reporting anything else here would let the filter and the field
                            // disagree with each other.
                            entry.IsBlockedOnInput,
                            missing,
                            entry.CreatedAt,
                            item.ThumbnailUrl
                        );
                    })
                    .ToArray();

                return new WishlistStoreGroup(
                    store.Id,
                    store.DisplayName,
                    store.IsActive,
                    store.LastSyncedAt,
                    storeEntries,
                    store.AvatarUrl
                );
            })
            .ToArray();

        // The wishlist serves its whole result in one slice, so the bounds describe exactly what was
        // returned. The envelope is still the shared one: a client reads it like every other list.
        return Result<WishlistGroupedResponse, WishlistError>.Success(
            new WishlistGroupedResponse(grouped, grouped.Length, Offset: 0, Limit: grouped.Length)
        );
    }
}
