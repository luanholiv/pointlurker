using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class StoreItemRepository(AppDbContext dbContext) : IStoreItemRepository
{
    public async Task<StoreItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.StoreItems.FirstOrDefaultAsync(item => item.Id == id, ct);
    }

    public async Task<StoreItem?> GetByIdWithStoreAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext
            .StoreItems.Include(item => item.WatchedStore)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
    }

    public async Task<StoreItem?> GetByProviderItemIdAsync(
        ProviderItemId providerItemId,
        StoreExternalId storeExternalId,
        CancellationToken ct = default
    )
    {
        return await dbContext.StoreItems.FirstOrDefaultAsync(
            item =>
                item.ProviderItemId == providerItemId && item.StoreExternalId == storeExternalId,
            ct
        );
    }

    public async Task<IReadOnlyList<StoreItem>> ListByStoreForUpdateAsync(
        Guid watchedStoreId,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .StoreItems.AsTracking()
            .Where(item => item.WatchedStoreId == watchedStoreId)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<StoreItemListRow> Items, int TotalCount)> ListAsync(
        Guid watchedStoreId,
        StoreItemFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    )
    {
        var query = Filter(
            dbContext.StoreItems.Where(item => item.WatchedStoreId == watchedStoreId),
            filter
        );

        var totalCount = await query.CountAsync(ct);

        // Projected in SQL; the wishlist identifier comes from a left join the projection creates,
        // because a projected query inherits no Include from anywhere else.
        //
        // `Enabled` travels rather than an availability boolean: the row derives that itself, through
        // the same `ItemAvailability.From` the domain uses, so this projection cannot state a third
        // version of the rule that drifts from `StoreItem.IsAvailableExpression`.
        var items = await Order(query, filter)
            .Skip(offset)
            .Take(limit)
            .Select(item => new StoreItemListRow(
                item.Id,
                item.ProviderItemId.Value,
                item.Name,
                item.Description,
                item.Cost,
                item.CategoryName,
                item.SubscriberOnly,
                item.Enabled,
                item.Quantity.Current,
                item.Quantity.Total,
                item.AllowMessages,
                item.ThumbnailUrl,
                item.WishlistEntry != null ? item.WishlistEntry.Id : null,
                item.ProviderCreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<StoreItemFacets> GetFacetsAsync(
        Guid watchedStoreId,
        CancellationToken ct = default
    )
    {
        // Deliberately unfiltered: these drive the filter controls, and narrowing them to the
        // current result would make the category list shrink as the user picks from it.
        var query = dbContext.StoreItems.Where(item => item.WatchedStoreId == watchedStoreId);

        var categories = await query
            .Select(item => item.CategoryName)
            .Distinct()
            .OrderBy(name => EF.Functions.Collate(name, SearchCollation.Name))
            .ToListAsync(ct);

        var bounds = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Min = group.Min(item => item.Cost),
                Max = group.Max(item => item.Cost),
            })
            .FirstOrDefaultAsync(ct);

        // A store with no items has no bounds at all rather than a bound of zero, but the contract
        // is a pair of numbers and the control renders nothing for an empty store either way.
        return new StoreItemFacets(categories, bounds?.Min ?? 0, bounds?.Max ?? 0);
    }

    private static IQueryable<StoreItem> Filter(IQueryable<StoreItem> query, StoreItemFilter filter)
    {
        if (filter.AvailableOnly)
        {
            // Composed, not restated: the invariant lives on the entity so the in-memory and the
            // SQL answer cannot drift apart.
            query = query.Where(StoreItem.IsAvailableExpression);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            query = MatchTerm(query, filter.SearchTerm);
        }

        if (filter.SubscriberOnly is { } subscriberOnly)
        {
            query = query.Where(item => item.SubscriberOnly == subscriberOnly);
        }

        if (filter.Featured is { } featured)
        {
            query = query.Where(item => item.Featured == featured);
        }

        if (filter.Categories is { Count: > 0 } categories)
        {
            query = query.Where(item => categories.Contains(item.CategoryName));
        }

        if (filter.MinCost is { } minCost)
        {
            query = query.Where(item => item.Cost >= minCost);
        }

        if (filter.MaxCost is { } maxCost)
        {
            query = query.Where(item => item.Cost <= maxCost);
        }

        return query;
    }

    /// <summary>
    /// Every way one term is allowed to find an item, OR-ed into a single narrowing so it still
    /// combines with the other filters conjunctively. The arms themselves live in
    /// <see cref="StoreItemTextMatch" />, because the wishlist matches items by the name ones too
    /// and the two lists have to agree about what a term finds.
    /// </summary>
    private static IQueryable<StoreItem> MatchTerm(IQueryable<StoreItem> query, string term) =>
        query.Where(StoreItemTextMatch.ByNameOrDescription(term));

    private static IOrderedQueryable<StoreItem> Order(
        IQueryable<StoreItem> query,
        StoreItemFilter filter
    )
    {
        var descending = filter.SortDirection == SortDirection.Desc;

        var ordered = filter.SortField switch
        {
            // Best-first regardless of `descending`, which is why this arm ignores it: the request
            // is for the closest answers, and the reverse of that is not something to ask for. A
            // relevance sort with no term falls through to the name arm — the value is one the enum
            // accepts, there is simply nothing to rank.
            StoreItemSortField.Relevance when !string.IsNullOrWhiteSpace(filter.SearchTerm) =>
                RankBy(query, filter.SearchTerm),

            StoreItemSortField.Cost => descending
                ? query.OrderByDescending(item => item.Cost)
                : query.OrderBy(item => item.Cost),

            // -1 means unlimited, so it has to sort as the largest value rather than the smallest.
            // Both clauses flip together: leaving the first ascending would put unlimited last in
            // both directions.
            StoreItemSortField.Stock => descending
                ? query
                    .OrderByDescending(item => item.Quantity.Current == -1 ? 1 : 0)
                    .ThenByDescending(item => item.Quantity.Current)
                : query
                    .OrderBy(item => item.Quantity.Current == -1 ? 1 : 0)
                    .ThenBy(item => item.Quantity.Current),

            StoreItemSortField.Date => descending
                ? query.OrderByDescending(item => item.ProviderCreatedAt)
                : query.OrderBy(item => item.ProviderCreatedAt),

            _ => descending
                ? query.OrderByDescending(item =>
                    EF.Functions.Collate(item.Name.ToLower(), SearchCollation.Name)
                )
                : query.OrderBy(item =>
                    EF.Functions.Collate(item.Name.ToLower(), SearchCollation.Name)
                ),
        };

        // Ascending even when the primary key descends. SQL ordering is not stable, so without a
        // tiebreaker offset paging duplicates some tied rows and skips others; and the client's
        // stable sort does not reverse ties, because negating a comparator's zero is still zero.
        return ordered.ThenBy(item => item.Id);
    }

    /// <summary>
    /// Orders a matched set by how well each item answers the term.
    /// </summary>
    /// <remarks>
    /// Two keys, in this order. First, whether the <em>name</em> is why the row is here: that is
    /// the text the grid renders, so an item found by its name comes before one found by shared
    /// boilerplate in its description, whatever the scores say.
    /// <para>
    /// Then the score itself, over one vector carrying both fields at different weights. The
    /// weights are what stop a description from outscoring a name simply by being longer, and the
    /// score is also what separates the arms inside the first key without spelling them out again:
    /// a substring or near-miss match contributes no lexeme, so it scores zero and settles below a
    /// genuine word match on the same name.
    /// </para>
    /// The name collation is the same one every other ordering uses, so a ranked list and an
    /// alphabetical one cannot disagree about how two names compare.
    /// </remarks>
    private static IOrderedQueryable<StoreItem> RankBy(IQueryable<StoreItem> query, string term)
    {
        var pattern = LikePattern.Containing(term);
        var nearMiss = StoreItemTextSearch.NearMissThreshold(term);

        return query
            .OrderBy(item =>
                EF.Functions.ToTsVector(StoreItemTextSearch.Configuration, item.Name)
                    .Matches(
                        EF.Functions.WebSearchToTsQuery(StoreItemTextSearch.Configuration, term)
                    )
                || EF.Functions.ILike(
                    EF.Functions.Unaccent(item.Name),
                    EF.Functions.Unaccent(pattern),
                    LikePattern.EscapeCharacter
                )
                || EF.Functions.TrigramsWordSimilarity(term, EF.Functions.Unaccent(item.Name))
                    >= nearMiss
                    ? 0
                    : 1
            )
            .ThenByDescending(item =>
                EF.Functions.ToTsVector(StoreItemTextSearch.Configuration, item.Name)
                    .SetWeight(NpgsqlTsVector.Lexeme.Weight.A)
                    .Concat(
                        EF.Functions.ToTsVector(
                                StoreItemTextSearch.Configuration,
                                item.Description ?? string.Empty
                            )
                            .SetWeight(NpgsqlTsVector.Lexeme.Weight.B)
                    )
                    .Rank(EF.Functions.WebSearchToTsQuery(StoreItemTextSearch.Configuration, term))
            )
            .ThenBy(item => EF.Functions.Collate(item.Name.ToLower(), SearchCollation.Name));
    }

    public void Upsert(IReadOnlyCollection<StoreItem> items)
    {
        // Tracking already answers "does this row exist?", so there is no ID probe: a tracked item
        // came from ListByStoreForUpdateAsync and saves as a delta, an untracked one is new. Unlike
        // Update, a detached item is not an error here — an *existing* row arriving detached surfaces
        // as a primary-key violation rather than silently writing nothing, which is the failure mode
        // worth having.
        foreach (var item in items)
        {
            if (!dbContext.IsTrackedForUpdate(item))
            {
                dbContext.StoreItems.Add(item);
            }
        }
    }
}
