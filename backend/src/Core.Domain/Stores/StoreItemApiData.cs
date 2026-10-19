using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Stores;

public sealed record StoreItemApiData(
    StoreProvider Provider,
    ProviderItemId ProviderItemId,
    StoreExternalId StoreExternalId,
    string Name,
    string? Description,
    string Type,
    long Cost,
    string CategoryName,
    bool Enabled,
    bool Featured,
    bool Public,
    bool SubscriberOnly,
    bool AllowMessages,
    IReadOnlyList<string> UserInputDefinitions,
    IReadOnlyList<string> Sources,
    ItemQuantity Quantity,
    ItemCooldown Cooldown,
    DateTimeOffset? ProviderCreatedAt,
    DateTimeOffset? ProviderUpdatedAt,
    DateTimeOffset LastFetchedAt,
    string? ThumbnailUrl = null,
    string? PreviewUrl = null
);
