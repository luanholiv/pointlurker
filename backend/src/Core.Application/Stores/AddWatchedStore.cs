using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

public sealed class AddWatchedStore(
    IWatchedStoreRepository storeRepository,
    IInputLabelResolver inputLabelResolver,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
)
{
    public async Task<Result<AddWatchedStoreResult, StoreError>> ExecuteAsync(
        Guid userId,
        StoreProvider provider,
        StoreExternalId externalId,
        string displayName,
        IReadOnlyList<StoreItemApiData> itemsData,
        string? avatarUrl = null,
        CancellationToken ct = default
    )
    {
        var exists = await storeRepository.ExistsByUserAndExternalIdAsync(
            userId,
            provider,
            externalId,
            ct
        );
        if (exists)
        {
            return Result<AddWatchedStoreResult, StoreError>.Failure(
                StoreError.AlreadyExists(externalId.Value)
            );
        }

        var now = timeProvider.GetUtcNow();
        var store = new WatchedStore(
            Guid.NewGuid(),
            provider,
            externalId,
            displayName,
            userId,
            now,
            avatarUrl
        );

        foreach (var data in itemsData)
        {
            var item = new StoreItem(Guid.NewGuid(), store.Id, data);
            store.AddItem(item);
        }

        store.RecordSync(now);
        storeRepository.Add(store);
        await unitOfWork.SaveChangesAsync(ct);

        // No reload: the store and its items were tracked and saved as this very graph, and nothing is
        // database-generated. Its items are attached above, the input profile is legitimately empty
        // for a store created a moment ago, and the one navigation a reload would add — each item's
        // wishlist entry — cannot exist yet.
        var missing = InputCoverage.CollectStoreMissingLabels(store, inputLabelResolver);

        return Result<AddWatchedStoreResult, StoreError>.Success(
            new AddWatchedStoreResult(store, missing)
        );
    }
}

public sealed record AddWatchedStoreResult(WatchedStore Store, IReadOnlyList<string> MissingLabels);
