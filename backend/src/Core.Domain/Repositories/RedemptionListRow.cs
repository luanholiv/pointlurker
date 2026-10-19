using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// One row of the redemption history, as read. The name and store are the snapshots the redemption
/// took when it was created, so this needs no join and stays correct after the item is renamed or the
/// store is removed.
/// </summary>
public sealed record RedemptionListRow(
    Guid Id,
    RedemptionStatus Status,
    string StoreItemName,
    string StoreName,
    DateTimeOffset AttemptedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? SettledAt,
    SubmissionFailureCode? FailureCode,
    string? FailureContext,
    /// <summary>
    /// Consecutive failed completion checks. Non-zero means the row is submitted and its outcome
    /// cannot currently be read — the one thing that separates a row still waiting upstream from
    /// one whose status check is broken. The reason itself stays server-side, in the logs.
    /// </summary>
    int PollFailures,
    StoreProvider Provider
);
