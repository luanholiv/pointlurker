using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class RedemptionRepository(AppDbContext dbContext) : IRedemptionRepository
{
    public void Add(Redemption redemption) => dbContext.Redemptions.Add(redemption);

    public void Update(Redemption redemption) =>
        dbContext.EnsureTrackedForUpdate(
            redemption,
            $"{nameof(GetPendingForUpdateAsync)}/{nameof(GetSubmittedForUpdateAsync)}"
        );

    public async Task<IReadOnlyList<Redemption>> GetPendingForUpdateAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .Redemptions.AsTracking()
            .Where(r =>
                r.Status == RedemptionStatus.Pending
                && (r.NextRetryAt == null || r.NextRetryAt <= now)
            )
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Redemption>> GetSubmittedForUpdateAsync(
        CancellationToken ct = default
    )
    {
        return await dbContext
            .Redemptions.AsTracking()
            .Where(r => r.Status == RedemptionStatus.Submitted)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Redemption>> ListByWishlistEntryAsync(
        Guid wishlistEntryId,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .Redemptions.Where(r => r.WishlistEntryId == wishlistEntryId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, RedemptionQuotaUsage>> CountHoldingQuotaAsync(
        IReadOnlyCollection<Guid> wishlistEntryIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(wishlistEntryIds);

        if (wishlistEntryIds.Count == 0)
        {
            return ReadOnlyDictionary<Guid, RedemptionQuotaUsage>.Empty;
        }

        // Both counts come off one grouped scan: the inflight states are a subset of the quota
        // holding ones, so a second query would read the same rows again.
        var rows = await dbContext
            .Redemptions.Where(r =>
                wishlistEntryIds.Contains(r.WishlistEntryId)
                && RedemptionQuota.HoldingStatuses.Contains(r.Status)
            )
            .GroupBy(r => r.WishlistEntryId)
            .Select(group => new
            {
                WishlistEntryId = group.Key,
                QuotaUsed = group.Count(),
                Inflight = group.Count(r => RedemptionQuota.InflightStatuses.Contains(r.Status)),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            row => row.WishlistEntryId,
            row => new RedemptionQuotaUsage(row.QuotaUsed, row.Inflight)
        );
    }

    public async Task<(IReadOnlyList<RedemptionListRow> Items, int TotalCount)> ListAsync(
        RedemptionFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Redemptions.AsQueryable();

        if (filter.Status.HasValue)
            query = query.Where(r => r.Status == filter.Status.Value);

        if (filter.WishlistEntryId.HasValue)
            query = query.Where(r => r.WishlistEntryId == filter.WishlistEntryId.Value);

        if (filter.From.HasValue)
            query = query.Where(r => r.AttemptedAt >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(r => r.AttemptedAt <= filter.To.Value);

        var totalCount = await query.CountAsync(ct);

        // Projected in SQL: only the history's own columns are read, and the encrypted message
        // snapshot and channel/item identifiers this page never shows stay in the database. The
        // ordering is applied here, to the whole matching set, before the slice is taken.
        var items = await Order(query, filter)
            .Skip(offset)
            .Take(limit)
            .Select(r => new RedemptionListRow(
                r.Id,
                r.Status,
                r.StoreItemName,
                r.StoreName,
                r.AttemptedAt,
                r.SubmittedAt,
                r.SettledAt,
                r.FailureCode,
                r.FailureContext,
                r.PollFailures,
                r.Provider
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    private static IOrderedQueryable<Redemption> Order(
        IQueryable<Redemption> query,
        RedemptionFilter filter
    )
    {
        var descending = filter.SortDirection == SortDirection.Desc;

        var ordered = filter.SortField switch
        {
            // The one ordering here without `EF.Functions.Collate`, deliberately. `Status` is an
            // enum persisted through `HasConversion<string>()`, so the CLR operand is not a string
            // and `Collate` cannot take it — and it needs none: the stored values are ASCII enum
            // names, which every collation orders identically. `RedemptionOrderingTests` asserts
            // that order by name.
            RedemptionSortField.Status => descending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),

            RedemptionSortField.StoreItemName => descending
                ? query.OrderByDescending(r =>
                    EF.Functions.Collate(r.StoreItemName.ToLower(), SearchCollation.Name)
                )
                : query.OrderBy(r =>
                    EF.Functions.Collate(r.StoreItemName.ToLower(), SearchCollation.Name)
                ),

            RedemptionSortField.StoreName => descending
                ? query.OrderByDescending(r =>
                    EF.Functions.Collate(r.StoreName.ToLower(), SearchCollation.Name)
                )
                : query.OrderBy(r =>
                    EF.Functions.Collate(r.StoreName.ToLower(), SearchCollation.Name)
                ),

            // The two nullable timestamps: PostgreSQL sorts NULLs last ascending and first
            // descending by default, which is consistent and repeatable — the requirement is that
            // they group at one end, not which end.
            RedemptionSortField.SubmittedAt => descending
                ? query.OrderByDescending(r => r.SubmittedAt)
                : query.OrderBy(r => r.SubmittedAt),

            RedemptionSortField.SettledAt => descending
                ? query.OrderByDescending(r => r.SettledAt)
                : query.OrderBy(r => r.SettledAt),

            _ => descending
                ? query.OrderByDescending(r => r.AttemptedAt)
                : query.OrderBy(r => r.AttemptedAt),
        };

        // Ascending regardless of direction, for the same reason as every other list: SQL ordering
        // is not stable, and offset paging over tied keys otherwise repeats and skips rows.
        return ordered.ThenBy(r => r.Id);
    }
}
