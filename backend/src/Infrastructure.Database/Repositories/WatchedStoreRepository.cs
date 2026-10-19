using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class WatchedStoreRepository(AppDbContext dbContext) : IWatchedStoreRepository
{
    public async Task<WatchedStore?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext
            .WatchedStores.Include(store => store.Items)
                .ThenInclude(item => item.WishlistEntry)
            .FirstOrDefaultAsync(store => store.Id == id, ct);
    }

    public async Task<WatchedStore?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext
            .WatchedStores.AsTracking()
            .FirstOrDefaultAsync(store => store.Id == id, ct);
    }

    public async Task<IReadOnlyList<WatchedStore>> ListActiveAsync(CancellationToken ct = default)
    {
        return await dbContext.WatchedStores.Where(store => store.IsActive).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WatchedStore>> ListActiveForUpdateAsync(
        CancellationToken ct = default
    )
    {
        return await dbContext
            .WatchedStores.AsTracking()
            .Where(store => store.IsActive)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<WatchedStoreListRow> Items, int TotalCount)> ListByUserAsync(
        Guid userId,
        WatchedStoreFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = dbContext.WatchedStores.Where(store => store.UserId == userId);

        if (!string.IsNullOrWhiteSpace(filter.TextContains))
        {
            // Display name or store identifier. The identifier is the store id rather than the
            // channel handle: `ChannelId` is a value object behind a ValueConverter, and a provider
            // cannot translate a substring match over one — reaching the column with EF.Property
            // makes EF infer the StoreExternalId mapping for the pattern parameter, which then fails to
            // materialize.
            var pattern = LikePattern.Containing(filter.TextContains);
            query = query.Where(store =>
                EF.Functions.ILike(store.DisplayName, pattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(store.Id.ToString(), pattern, LikePattern.EscapeCharacter)
            );
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(store => store.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(store =>
                EF.Functions.Collate(store.DisplayName.ToLower(), SearchCollation.Name)
            )
            .ThenBy(store => store.Id)
            .Skip(offset)
            .Take(limit)
            .Select(store => new WatchedStoreListRow(
                store.Id,
                store.ExternalId.Value,
                store.DisplayName,
                store.IsActive,
                store.LastSyncedAt,
                store.Items.Count,
                store.AvatarUrl,
                store.Provider
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<WatchedStoreTotals> GetTotalsByUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        // Deliberately unfiltered, and counted in one round trip rather than two.
        var totals = await dbContext
            .WatchedStores.Where(store => store.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new WatchedStoreTotals(
                group.Count(store => store.IsActive),
                group.Count(store => !store.IsActive)
            ))
            .FirstOrDefaultAsync(ct);

        return totals ?? new WatchedStoreTotals(0, 0);
    }

    public async Task<bool> ExistsByUserAndExternalIdAsync(
        Guid userId,
        StoreProvider provider,
        StoreExternalId externalId,
        CancellationToken ct = default
    )
    {
        return await dbContext.WatchedStores.AnyAsync(
            store =>
                store.UserId == userId
                && store.Provider == provider
                && store.ExternalId == externalId,
            ct
        );
    }

    public async Task<WatchedStore?> GetProfileAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.WatchedStores.FirstOrDefaultAsync(store => store.Id == id, ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.WatchedStores.AnyAsync(store => store.Id == id, ct);
    }

    public async Task<WatchedStoreDetailRow?> GetDetailAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .WatchedStores.Where(store => store.Id == id)
            .Select(store => new WatchedStoreDetailRow(
                store.Id,
                store.ExternalId.Value,
                store.DisplayName,
                store.IsActive,
                store.LastSyncedAt,
                store.CreatedAt,
                store.Items.Count,
                store.AvatarUrl,
                store.Provider
            ))
            .FirstOrDefaultAsync(ct);
    }

    public void Add(WatchedStore store) =>
        dbContext.ChangeTracker.TrackGraph(store, node => node.Entry.State = EntityState.Added);

    public void Update(WatchedStore store) =>
        dbContext.EnsureTrackedForUpdate(store, nameof(GetForUpdateAsync));

    public void Remove(WatchedStore store) => dbContext.WatchedStores.Remove(store);
}
