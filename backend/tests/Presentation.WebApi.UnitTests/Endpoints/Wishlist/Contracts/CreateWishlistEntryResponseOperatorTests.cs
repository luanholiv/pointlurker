using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Contracts;

public sealed class CreateWishlistEntryResponseOperatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_from_both_the_entry_and_the_item()
    {
        var item = new StoreItem(Guid.NewGuid(), Guid.NewGuid(), ApiData());
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        entry.EnableAutoRedeem(Now);

        var response = (CreateWishlistEntryResponse)new AddWishlistEntrySuccess(entry, item);

        response.Id.Should().Be(entry.Id);
        response.StoreItemId.Should().Be(item.Id);
        response.ItemName.Should().Be("Shoutout");
        response.NotifyOnAvailable.Should().BeTrue();
        response.AutoRedeem.Should().BeTrue();
        response.CreatedAt.Should().Be(Now);
    }

    /// <summary>
    /// The store name comes off the WatchedStore navigation, which is why AddWishlistEntry loads
    /// the item with GetByIdWithStoreAsync. A caller that skips the include degrades to an empty
    /// name rather than throwing. The populated case needs a real EF graph, so it is asserted
    /// end-to-end in IntegrationTests/Wishlist/WishlistEndpointTests.
    /// </summary>
    [Fact]
    public void Falls_back_to_an_empty_store_name_when_the_store_was_not_loaded()
    {
        var item = new StoreItem(Guid.NewGuid(), Guid.NewGuid(), ApiData());
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);

        var response = (CreateWishlistEntryResponse)new AddWishlistEntrySuccess(entry, item);

        response.StoreName.Should().BeEmpty();
        response.ItemName.Should().Be("Shoutout");
    }

    private static StoreItemApiData ApiData() =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("channel-1"),
            "Shoutout",
            "A shoutout",
            "perk",
            500,
            "default",
            Enabled: true,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: true,
            UserInputDefinitions: [],
            Sources: [],
            Quantity: new ItemQuantity(-1, -1),
            Cooldown: new ItemCooldown(0, 0, 0),
            ProviderCreatedAt: Now,
            ProviderUpdatedAt: null,
            LastFetchedAt: Now
        );
}
