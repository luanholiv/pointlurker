using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

public interface IWishlistEntryRepository
{
    Task<WishlistEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The entry, change-tracked, for a caller that is about to mutate and save it.</summary>
    Task<WishlistEntry?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<WishlistEntry?> GetByStoreItemAsync(
        Guid userId,
        Guid storeItemId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Auto-redeem entries with their item and that item's store, change-tracked: the caller creates a
    /// redemption from each entry it acts on, which stamps the entry's cooldown checkpoint.
    /// </summary>
    Task<IReadOnlyList<WishlistEntry>> ListAutoRedeemWithItemsForUpdateAsync(
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// A user's wishlist entries with their item and that item's store, narrowed and ordered by the
    /// database. Entries arrive ordered by store display name first, so a caller can group them by
    /// walking the sequence rather than sorting it again.
    /// </summary>
    Task<IReadOnlyList<WishlistEntry>> ListByUserWithItemsAsync(
        Guid userId,
        WishlistEntryFilter filter,
        CancellationToken ct = default
    );

    /// <summary>
    /// A store's entries with their items, change-tracked: the caller re-evaluates input coverage and
    /// blocks or unblocks each entry. The item's store is not loaded — the caller already holds it.
    /// </summary>
    Task<IReadOnlyList<WishlistEntry>> ListByStoreWithItemsForUpdateAsync(
        Guid storeId,
        CancellationToken ct = default
    );

    /// <summary>Stages the entry for insertion.</summary>
    void Add(WishlistEntry entry);

    /// <summary>
    /// Declares that <paramref name="entry"/> is being written, and throws if it is not tracked —
    /// saving a detached entity writes nothing while looking like success. Stages nothing itself: the
    /// mutation is already in the change tracker.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entry came from an untracked read.</exception>
    void Update(WishlistEntry entry);

    /// <summary>Stages the entry for deletion.</summary>
    void Remove(WishlistEntry entry);
}
