using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Reads how many of a caller's stores are active and how many are paused. Takes no filter by
/// design — see <see cref="WatchedStoreTotals"/>.
/// </summary>
public sealed class GetWatchedStoreTotals(IWatchedStoreRepository storeRepository)
{
    public async Task<Result<WatchedStoreTotals, StoreError>> ExecuteAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        Result<WatchedStoreTotals, StoreError>.Success(
            await storeRepository.GetTotalsByUserAsync(userId, ct)
        );
}
