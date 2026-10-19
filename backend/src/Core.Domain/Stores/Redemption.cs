using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Stores;

public sealed class Redemption
{
    private Redemption() { }

    internal Redemption(
        Guid id,
        Guid wishlistEntryId,
        Guid storeItemId,
        string storeItemName,
        string storeName,
        StoreProvider provider,
        StoreExternalId storeExternalId,
        ProviderItemId providerItemId,
        string? messageSnapshot,
        DateTimeOffset attemptedAt
    )
    {
        Id = id;
        WishlistEntryId = wishlistEntryId;
        StoreItemId = storeItemId;
        StoreItemName = storeItemName;
        StoreName = storeName;
        Provider = provider;
        StoreExternalId = storeExternalId;
        ProviderItemId = providerItemId;
        MessageSnapshot = messageSnapshot;
        Status = RedemptionStatus.Pending;
        Attempts = 0;
        AttemptedAt = attemptedAt;
    }

    public Guid Id { get; private set; }
    public Guid WishlistEntryId { get; private set; }
    public Guid StoreItemId { get; private set; }
    public string StoreItemName { get; private set; } = "";
    public string StoreName { get; private set; } = "";
    public StoreProvider Provider { get; private set; }
    public StoreExternalId StoreExternalId { get; private set; }
    public ProviderItemId ProviderItemId { get; private set; }
    public string? MessageSnapshot { get; private set; }
    public string? ProviderRedemptionId { get; private set; }
    public RedemptionStatus Status { get; private set; }

    /// <summary>Typed failure code persisted as int. Nullable while status is not in a failure terminal.</summary>
    public SubmissionFailureCode? FailureCode { get; private set; }

    /// <summary>Non-localizable diagnostic detail (e.g., upstream HTTP status text). Never shown to end users.</summary>
    public string? FailureContext { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int Attempts { get; private set; }

    /// <summary>
    /// Consecutive failed attempts to read this redemption's completion state from its provider.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Attempts"/>, which counts submissions. A failing status check says
    /// nothing about whether the redemption itself succeeded, so it must never masquerade as a
    /// submission failure — it is counted and surfaced instead.
    /// </remarks>
    public int PollFailures { get; private set; }

    /// <summary>Non-localizable detail of the most recent failed status check. Never shown to end users.</summary>
    public string? LastPollError { get; private set; }

    /// <param name="providerRedemptionId">
    /// The provider's id for the redemption, or null for a provider that returns none — GauPoints
    /// accepts a redemption without naming it, and nothing can read it back.
    /// </param>
    public void MarkSubmitted(string? providerRedemptionId, DateTimeOffset submittedAt)
    {
        if (Status != RedemptionStatus.Pending)
            throw new InvalidOperationException($"Cannot mark as Submitted from status {Status}.");

        ProviderRedemptionId = providerRedemptionId;
        SubmittedAt = submittedAt;
        Attempts++;
        Status = RedemptionStatus.Submitted;
    }

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        if (Status != RedemptionStatus.Submitted)
            throw new InvalidOperationException($"Cannot mark as Completed from status {Status}.");

        SettledAt = completedAt;
        Status = RedemptionStatus.Completed;
    }

    /// <summary>
    /// Marks this redemption as <see cref="RedemptionStatus.SubmissionFailed"/> with a typed failure code.
    /// </summary>
    public void MarkFailed(SubmissionFailureCode code, string? context = null)
    {
        if (
            Status
            is RedemptionStatus.Completed
                or RedemptionStatus.SubmissionFailed
                or RedemptionStatus.CompletionTimedOut
                or RedemptionStatus.Rejected
        )
            throw new InvalidOperationException(
                $"Cannot mark as SubmissionFailed from terminal status {Status}."
            );

        FailureCode = code;
        FailureContext = context;
        Status = RedemptionStatus.SubmissionFailed;
    }

    public void ScheduleRetry(DateTimeOffset nextRetryAt)
    {
        if (Status != RedemptionStatus.Pending)
            throw new InvalidOperationException($"Cannot schedule retry from status {Status}.");

        NextRetryAt = nextRetryAt;
        Attempts++;
    }

    public void MarkTimedOut()
    {
        if (Status != RedemptionStatus.Submitted)
            throw new InvalidOperationException($"Cannot mark as timed out from status {Status}.");

        Status = RedemptionStatus.CompletionTimedOut;
    }

    /// <summary>
    /// Records that the provider refused to fulfil an accepted submission.
    /// </summary>
    /// <remarks>
    /// A refusal is a known end state, so it is terminal rather than something to keep polling.
    /// <paramref name="rejectedAt"/> is the upstream update time, stored in <see cref="SettledAt"/>
    /// just as a completion time is: both answer "when did the provider settle this".
    /// </remarks>
    public void MarkRejected(DateTimeOffset rejectedAt)
    {
        if (Status != RedemptionStatus.Submitted)
            throw new InvalidOperationException($"Cannot mark as rejected from status {Status}.");

        SettledAt = rejectedAt;
        Status = RedemptionStatus.Rejected;
    }

    /// <summary>
    /// Records that this redemption's completion state could not be read from its provider.
    /// </summary>
    /// <remarks>
    /// Deliberately not a status change. The submission itself succeeded, and re-opening the
    /// redemption would let auto-redeem submit a second one — spending the owner's points again for
    /// an item that may already be redeemed upstream. The count exists so a permanently broken
    /// status check is visible while it happens, instead of being indistinguishable from work still
    /// pending until the completion timeout expires.
    /// </remarks>
    public void RecordPollFailure(string error)
    {
        if (Status != RedemptionStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot record a poll failure from status {Status}."
            );

        PollFailures++;
        LastPollError = error.Length > MaxPollErrorLength ? error[..MaxPollErrorLength] : error;
    }

    /// <summary>
    /// Clears the failed-status-check trail after a reading succeeds, and reports whether there was
    /// anything to clear so the caller can skip a write on a healthy tick.
    /// </summary>
    public bool ClearPollFailures()
    {
        if (PollFailures == 0 && LastPollError is null)
            return false;

        PollFailures = 0;
        LastPollError = null;
        return true;
    }

    /// <summary>Matches the persisted column width; see the entity configuration.</summary>
    private const int MaxPollErrorLength = 512;
}
