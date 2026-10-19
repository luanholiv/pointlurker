namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// How often one raw input label has failed to resolve for a store, and how close it came. Aggregated
/// over the resolution log, so it describes a label's history rather than its current outcome.
/// </summary>
public sealed record UnresolvedLabelSummary(
    string RawLabel,
    int BestScore,
    DateTimeOffset LastObservedAt,
    int OccurrenceCount
);
