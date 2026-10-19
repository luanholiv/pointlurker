using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One catalogue item as a provider reports it, before the fetch instant is attached. Projected onto
/// the domain's payload by <see cref="ProviderStoreItemMapping.ToApiData"/>.
/// </summary>
public sealed record ProviderStoreItem(
    StoreProvider Provider,
    ProviderItemId Id,
    StoreExternalId Store,
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
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ThumbnailUrl = null,
    string? PreviewUrl = null
);
