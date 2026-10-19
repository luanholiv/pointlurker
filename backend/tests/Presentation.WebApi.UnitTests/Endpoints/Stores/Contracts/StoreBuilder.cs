using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Contracts;

/// <summary>
/// Minimal domain aggregates for the store contract operators, which map from
/// <see cref="WatchedStore"/> and <see cref="StoreItem"/> rather than an application record.
/// </summary>
internal static class StoreBuilder
{
    public static readonly DateTimeOffset Created = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    public static WatchedStore Store(string? avatarUrl = null) =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("channel-1"),
            "xQc Store",
            Guid.NewGuid(),
            Created,
            avatarUrl
        );

    public static StoreItem Item(
        Guid watchedStoreId,
        string name = "Shoutout",
        bool enabled = true,
        ItemQuantity? quantity = null,
        IReadOnlyList<string>? userInputDefinitions = null,
        string? thumbnailUrl = null,
        string? previewUrl = null
    ) =>
        new(
            Guid.NewGuid(),
            watchedStoreId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-" + name.ToLowerInvariant()),
                new StoreExternalId("channel-1"),
                name,
                "A " + name,
                "perk",
                500,
                "default",
                Enabled: enabled,
                Featured: false,
                Public: true,
                SubscriberOnly: false,
                AllowMessages: true,
                UserInputDefinitions: userInputDefinitions ?? [],
                Sources: [],
                Quantity: quantity ?? new ItemQuantity(-1, -1),
                Cooldown: new ItemCooldown(0, 0, 0),
                ProviderCreatedAt: Created,
                ProviderUpdatedAt: null,
                LastFetchedAt: Created,
                ThumbnailUrl: thumbnailUrl,
                PreviewUrl: previewUrl
            )
        );
}
