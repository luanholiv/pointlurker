using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Repositories;

public interface IStoreItemRepository
{
    Task<StoreItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StoreItem?> GetByIdWithStoreAsync(Guid id, CancellationToken ct = default);
    Task<StoreItem?> GetByProviderItemIdAsync(
        ProviderItemId providerItemId,
        StoreExternalId storeExternalId,
        CancellationToken ct = default
    );

    /// <summary>
    /// A store's items, change-tracked: the caller folds the upstream payload into them and stages them
    /// via <see cref="Upsert"/>, which distinguishes these from freshly constructed items by tracking.
    /// </summary>
    Task<IReadOnlyList<StoreItem>> ListByStoreForUpdateAsync(
        Guid watchedStoreId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Stages a mixed batch: tracked items were loaded by
    /// <see cref="ListByStoreForUpdateAsync"/> and are written as deltas, untracked ones are new and
    /// are inserted.
    /// </summary>
    void Upsert(IReadOnlyCollection<StoreItem> items);

    /// <summary>
    /// One slice of a store's item grid, narrowed and ordered by the database, alongside the total
    /// number of items matching the filter — which is what tells a client whether more remain.
    /// </summary>
    Task<(IReadOnlyList<StoreItemListRow> Items, int TotalCount)> ListAsync(
        Guid watchedStoreId,
        StoreItemFilter filter,
        int offset,
        int limit,
        CancellationToken ct = default
    );

    /// <summary>
    /// The values the store's filter controls offer, computed across every one of its items and
    /// deliberately ignoring any active filter — see <see cref="StoreItemFacets"/>.
    /// </summary>
    Task<StoreItemFacets> GetFacetsAsync(Guid watchedStoreId, CancellationToken ct = default);
}
