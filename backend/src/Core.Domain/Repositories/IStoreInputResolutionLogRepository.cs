using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

public interface IStoreInputResolutionLogRepository
{
    /// <summary>Stages one log entry for insertion.</summary>
    void Add(StoreInputResolutionLogEntry entry);

    /// <summary>Stages a batch of log entries for insertion.</summary>
    void AddRange(IEnumerable<StoreInputResolutionLogEntry> entries);

    Task<IReadOnlyList<UnresolvedLabelSummary>> ListUnresolvedAsync(
        Guid storeId,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, LatestLabelOutcome>> GetLatestPerLabelAsync(
        Guid storeId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Deletes superseded log rows. A set-based delete, so unlike every other write here it bypasses
    /// the change tracker and commits on its own — it is not part of the caller's unit of work.
    /// </summary>
    Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
