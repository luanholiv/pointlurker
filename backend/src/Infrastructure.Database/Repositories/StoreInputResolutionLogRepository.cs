using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class StoreInputResolutionLogRepository(AppDbContext dbContext)
    : IStoreInputResolutionLogRepository
{
    // Add rather than AddAsync: the async overloads exist for database-side value generators, and
    // these keys are Guids assigned by the caller.
    public void Add(StoreInputResolutionLogEntry entry) =>
        dbContext.StoreInputResolutionLog.Add(entry);

    public void AddRange(IEnumerable<StoreInputResolutionLogEntry> entries) =>
        dbContext.StoreInputResolutionLog.AddRange(entries);

    public async Task<IReadOnlyList<UnresolvedLabelSummary>> ListUnresolvedAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        // Project the grouped aggregates into an anonymous type: EF Core can
        // translate GroupBy aggregates into a SQL GROUP BY this way, but NOT
        // when they are arguments to a record/constructor (it falls back to
        // client evaluation and throws). Map to the record and order in memory.
        var grouped = await dbContext
            .StoreInputResolutionLog.Where(e =>
                e.StoreId == storeId && e.Source == ResolutionSource.Unresolved
            )
            .GroupBy(e => e.RawLabel)
            .Select(g => new
            {
                RawLabel = g.Key,
                BestScore = g.Max(e => e.Confidence),
                LastObservedAt = g.Max(e => e.ObservedAt),
                OccurrenceCount = g.Count(),
            })
            .ToListAsync(ct);

        return grouped
            .OrderByDescending(s => s.LastObservedAt)
            .Select(s => new UnresolvedLabelSummary(
                s.RawLabel,
                s.BestScore,
                s.LastObservedAt,
                s.OccurrenceCount
            ))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, LatestLabelOutcome>> GetLatestPerLabelAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        // Same anonymous-type constraint as ListUnresolvedAsync: keep the
        // grouped projection record-free so it translates to SQL.
        var latest = await dbContext
            .StoreInputResolutionLog.Where(e => e.StoreId == storeId)
            .GroupBy(e => e.RawLabel)
            .Select(g =>
                g.OrderByDescending(e => e.ObservedAt)
                    .Select(e => new
                    {
                        e.RawLabel,
                        e.CanonicalLabel,
                        e.Source,
                        e.Confidence,
                    })
                    .First()
            )
            .ToListAsync(ct);

        var result = new Dictionary<string, LatestLabelOutcome>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in latest)
        {
            result[row.RawLabel] = new LatestLabelOutcome(
                row.CanonicalLabel,
                row.Source,
                row.Confidence
            );
        }

        return result;
    }

    public async Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        // Deletes superseded rows only: the newest row per (StoreId, RawLabel)
        // survives regardless of age so the label's current outcome stays
        // derivable. Translates to DELETE ... WHERE EXISTS.
        return await dbContext
            .StoreInputResolutionLog.Where(e =>
                e.ObservedAt < cutoff
                && dbContext.StoreInputResolutionLog.Any(n =>
                    n.StoreId == e.StoreId
                    && n.RawLabel == e.RawLabel
                    && n.ObservedAt > e.ObservedAt
                )
            )
            .ExecuteDeleteAsync(ct);
    }
}
