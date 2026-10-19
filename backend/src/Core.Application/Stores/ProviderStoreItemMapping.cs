using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// The single projection of a provider's item onto the shape the domain accepts. Both entry points
/// that ingest items — adding a store and syncing one — go through this, so the item shape is
/// described once.
/// </summary>
public static class ProviderStoreItemMapping
{
    /// <param name="fetchedAt">
    /// The instant the payload was observed. Supplied by the caller rather than read from the clock
    /// here so every item of one fetch shares a single <c>LastFetchedAt</c>.
    /// </param>
    public static StoreItemApiData ToApiData(this ProviderStoreItem item, DateTimeOffset fetchedAt)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new StoreItemApiData(
            item.Provider,
            item.Id,
            item.Store,
            item.Name,
            item.Description,
            item.Type,
            item.Cost,
            item.CategoryName,
            item.Enabled,
            item.Featured,
            item.Public,
            item.SubscriberOnly,
            item.AllowMessages,
            item.UserInputDefinitions,
            item.Sources,
            item.Quantity,
            item.Cooldown,
            item.CreatedAt,
            item.UpdatedAt,
            fetchedAt,
            item.ThumbnailUrl,
            item.PreviewUrl
        );
    }
}
