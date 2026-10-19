using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Refreshes the display name and avatar of every active store from its provider.
/// <para>
/// This is cosmetic data that changes approximately never, so it is refreshed on the daily
/// refresh schedule rather than on every sync tick — a per-tick lookup cost one extra
/// StreamElements call per store per minute. Failure-tolerant per store: a channel that cannot be
/// looked up keeps the name and avatar already stored, and the remaining stores still refresh.
/// </para>
/// </summary>
public sealed class RefreshStoreChannelInfo(
    IWatchedStoreRepository storeRepo,
    IStoreProviderClientFactory providers,
    IUnitOfWork unitOfWork,
    ILogger<RefreshStoreChannelInfo> logger
)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var stores = await storeRepo.ListActiveForUpdateAsync(ct);
        var refreshed = 0;

        foreach (var store in stores)
        {
            var channelId = store.ExternalId.Value;

            try
            {
                var client = providers.For(store.Provider);

                // Fixed name and avatar: there is nothing upstream to refresh them from.
                if (!client.Capabilities.ProvidesChannelMetadata)
                {
                    continue;
                }

                var lookup = await client.LookupStoreAsync(channelId, ct);
                if (lookup is null)
                {
                    continue;
                }

                store.UpdateChannelInfo(lookup.DisplayName, lookup.AvatarUrl);
                storeRepo.Update(store);
                refreshed++;
            }
            catch (Exception ex)
            {
                logger.LogChannelRefreshFailed(store.Id, channelId, ex);
            }
        }

        if (refreshed == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogChannelInfoRefreshed(refreshed);
    }
}
