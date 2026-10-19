using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class WishlistEntryRepository(AppDbContext dbContext) : IWishlistEntryRepository
{
    public async Task<WishlistEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.WishlistEntries.FirstOrDefaultAsync(entry => entry.Id == id, ct);
    }

    public async Task<WishlistEntry?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext
            .WishlistEntries.AsTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id, ct);
    }

    public async Task<WishlistEntry?> GetByStoreItemAsync(
        Guid userId,
        Guid storeItemId,
        CancellationToken ct = default
    )
    {
        return await dbContext.WishlistEntries.FirstOrDefaultAsync(
            entry => entry.UserId == userId && entry.StoreItemId == storeItemId,
            ct
        );
    }

    public async Task<IReadOnlyList<WishlistEntry>> ListAutoRedeemWithItemsForUpdateAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .WishlistEntries.AsTracking()
            .Where(entry => entry.UserId == userId && entry.AutoRedeem)
            .Include(entry => entry.StoreItem!)
                .ThenInclude(item => item.WatchedStore)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WishlistEntry>> ListByUserWithItemsAsync(
        Guid userId,
        WishlistEntryFilter filter,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .WishlistEntries.Where(entry => entry.UserId == userId)
            .Include(entry => entry.StoreItem!)
                .ThenInclude(item => item.WatchedStore)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            // Name arms only, and composed like the availability rule below: the match is written
            // over an item, so reaching it from an entry query is a semi-join.
            var matchingItemIds = dbContext
                .StoreItems.Where(StoreItemTextMatch.ByName(filter.SearchTerm))
                .Select(item => item.Id);

            query = query.Where(entry => matchingItemIds.Contains(entry.StoreItemId));
        }

        if (filter.AvailableOnly == true)
        {
            // Composed rather than restated: the availability rule stays the one on StoreItem, and
            // this becomes a semi-join the database evaluates.
            var availableItemIds = dbContext
                .StoreItems.Where(StoreItem.IsAvailableExpression)
                .Select(item => item.Id);

            query = query.Where(entry => availableItemIds.Contains(entry.StoreItemId));
        }

        if (filter.AutoRedeem is { } autoRedeem)
        {
            query = query.Where(entry => entry.AutoRedeem == autoRedeem);
        }

        if (filter.NotifyOnAvailable is { } notify)
        {
            query = query.Where(entry => entry.NotifyOnAvailable == notify);
        }

        if (filter.BlockedOnInput is { } blocked)
        {
            // The persisted flag, never a per-read recompute — a filter cannot be evaluated in SQL
            // against something the database does not know.
            query = query.Where(entry => entry.IsBlockedOnInput == blocked);
        }

        if (filter.StoreId is { } storeId)
        {
            query = query.Where(entry => entry.StoreItem!.WatchedStoreId == storeId);
        }

        // Store name first, so the caller groups by walking the sequence instead of re-sorting it.
        var ordered = query.OrderBy(entry =>
            EF.Functions.Collate(
                entry.StoreItem!.WatchedStore!.DisplayName.ToLower(),
                SearchCollation.Name
            )
        );

        return await Order(ordered, filter).ThenBy(entry => entry.Id).ToListAsync(ct);
    }

    private static IOrderedQueryable<WishlistEntry> Order(
        IOrderedQueryable<WishlistEntry> query,
        WishlistEntryFilter filter
    )
    {
        var descending = filter.SortDirection == SortDirection.Desc;

        return filter.SortField switch
        {
            WishlistEntrySortField.ItemCost => descending
                ? query.ThenByDescending(entry => entry.StoreItem!.Cost)
                : query.ThenBy(entry => entry.StoreItem!.Cost),

            WishlistEntrySortField.CreatedAt => descending
                ? query.ThenByDescending(entry => entry.CreatedAt)
                : query.ThenBy(entry => entry.CreatedAt),

            _ => descending
                ? query.ThenByDescending(entry =>
                    EF.Functions.Collate(entry.StoreItem!.Name.ToLower(), SearchCollation.Name)
                )
                : query.ThenBy(entry =>
                    EF.Functions.Collate(entry.StoreItem!.Name.ToLower(), SearchCollation.Name)
                ),
        };
    }

    public async Task<IReadOnlyList<WishlistEntry>> ListByStoreWithItemsForUpdateAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        // No ThenInclude(WatchedStore): the coverage evaluation reads the item's name and input
        // definitions, and already holds the store it is evaluating. Loading it again here would also
        // track a second instance of a store the caller is not writing through.
        return await dbContext
            .WishlistEntries.AsTracking()
            .Where(entry => entry.StoreItem!.WatchedStoreId == storeId)
            .Include(entry => entry.StoreItem!)
            .ToListAsync(ct);
    }

    public void Add(WishlistEntry entry) => dbContext.WishlistEntries.Add(entry);

    public void Update(WishlistEntry entry) =>
        dbContext.EnsureTrackedForUpdate(entry, nameof(GetForUpdateAsync));

    public void Remove(WishlistEntry entry) => dbContext.WishlistEntries.Remove(entry);
}
