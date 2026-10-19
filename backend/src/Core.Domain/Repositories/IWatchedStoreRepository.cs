using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Repositories;

public interface IWatchedStoreRepository
{
    Task<WatchedStore?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The store row alone, change-tracked, for a caller that is about to mutate and save it. Loads
    /// no items: every write path either ignores them or lets the database cascade handle them, and
    /// the graph the read methods pull would otherwise be written back untouched.
    /// </summary>
    Task<WatchedStore?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WatchedStore>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// The active store rows, change-tracked, for a caller about to mutate and save them all in one
    /// pass. Loads no items, for the same reason <see cref="GetForUpdateAsync"/> does not.
    /// </summary>
    Task<IReadOnlyList<WatchedStore>> ListActiveForUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// One slice of a user's stores, narrowed and ordered by the database, with the total number
    /// matching the filter.
    /// </summary>
    Task<(IReadOnlyList<WatchedStoreListRow> Items, int TotalCount)> ListByUserAsync(
        Guid userId,
        WatchedStoreFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    );

    /// <summary>
    /// Active and paused counts across every store the user owns, deliberately taking no filter —
    /// see <see cref="WatchedStoreTotals"/>.
    /// </summary>
    Task<WatchedStoreTotals> GetTotalsByUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ExistsByUserAndExternalIdAsync(
        Guid userId,
        StoreProvider provider,
        StoreExternalId externalId,
        CancellationToken ct = default
    );
    Task<WatchedStore?> GetProfileAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Whether the store exists at all. Reading a store's items needs to answer <c>404</c> for an
    /// unknown store without pulling the item graph <see cref="GetByIdAsync"/> loads — which is the
    /// very data the caller is about to fetch a filtered slice of.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The store's envelope and its unfiltered item count, counted in SQL. The items themselves are
    /// served by their own endpoint, so this read must not pull them.
    /// </summary>
    Task<WatchedStoreDetailRow?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Stages the store and its items for insertion.</summary>
    void Add(WatchedStore store);

    /// <summary>
    /// Declares that <paramref name="store"/> is being written, and throws if it is not tracked —
    /// saving a detached entity writes nothing while looking like success. Stages nothing itself: the
    /// mutation is already in the change tracker.
    /// </summary>
    /// <exception cref="InvalidOperationException">The store came from an untracked read.</exception>
    void Update(WatchedStore store);

    /// <summary>Stages the store for deletion; its items go by the database cascade.</summary>
    void Remove(WatchedStore store);
}
