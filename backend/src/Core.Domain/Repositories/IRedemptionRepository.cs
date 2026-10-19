using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

public interface IRedemptionRepository
{
    /// <summary>Stages the redemption for insertion.</summary>
    void Add(Redemption redemption);

    /// <summary>
    /// Declares that <paramref name="redemption"/> is being written, and throws if it is not tracked —
    /// saving a detached entity writes nothing while looking like success. Stages nothing itself: the
    /// mutation is already in the change tracker.
    /// </summary>
    /// <exception cref="InvalidOperationException">The redemption came from an untracked read.</exception>
    void Update(Redemption redemption);

    /// <summary>
    /// Redemptions due for submission, change-tracked: the caller drives each one to a terminal or
    /// retry state and saves it.
    /// </summary>
    Task<IReadOnlyList<Redemption>> GetPendingForUpdateAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    );

    /// <summary>
    /// Redemptions awaiting an upstream outcome, change-tracked: the caller completes or times out
    /// each one and saves it.
    /// </summary>
    Task<IReadOnlyList<Redemption>> GetSubmittedForUpdateAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Redemption>> ListByWishlistEntryAsync(
        Guid wishlistEntryId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Per entry, how many of its redemptions hold quota and how many are still in flight. Entries
    /// with neither are absent from the dictionary, so a caller reads a missing key as zero.
    /// <para>
    /// Batched on purpose: auto-redeem asks about every entry it is about to consider, and asking
    /// per entry puts a round trip inside the cycle's hot loop.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, RedemptionQuotaUsage>> CountHoldingQuotaAsync(
        IReadOnlyCollection<Guid> wishlistEntryIds,
        CancellationToken ct = default
    );

    /// <summary>
    /// A page of the redemption history, projected in SQL to the columns the history actually shows.
    /// Returns rows rather than aggregates: nothing downstream mutates a redemption, and materialising
    /// entities for a read-only list is what the read/write split exists to avoid.
    /// </summary>
    Task<(IReadOnlyList<RedemptionListRow> Items, int TotalCount)> ListAsync(
        RedemptionFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}
