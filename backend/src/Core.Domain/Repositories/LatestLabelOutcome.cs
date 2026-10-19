using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The most recent resolution recorded for one raw input label: what it resolved to, by what means,
/// and how confidently. Compared against a fresh resolution to decide whether the outcome changed and
/// therefore warrants a new log row.
/// </summary>
public sealed record LatestLabelOutcome(
    string? CanonicalLabel,
    ResolutionSource Source,
    int Confidence
);
