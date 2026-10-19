using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Contracts;

public sealed class StoreItemDetailResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set()
    {
        var item = StoreBuilder.Item(
            Guid.NewGuid(),
            "Shoutout",
            userInputDefinitions: ["Discord tag"],
            thumbnailUrl: "https://cdn.example/thumb.png",
            previewUrl: "https://cdn.example/preview.png"
        );

        var response = (StoreItemDetailResponse)item;

        response
            .Should()
            .BeEquivalentTo(
                new StoreItemDetailResponse(
                    Id: item.Id,
                    ProviderItemId: "se-shoutout",
                    Name: "Shoutout",
                    Description: "A Shoutout",
                    Type: "perk",
                    Cost: 500,
                    CategoryName: "default",
                    Enabled: true,
                    Featured: false,
                    IsPublic: true,
                    SubscriberOnly: false,
                    AllowMessages: true,
                    UserInputDefinitions: ["Discord tag"],
                    QuantityTotal: -1,
                    QuantityCurrent: -1,
                    IsAvailable: true,
                    WishlistEntryId: null,
                    ThumbnailUrl: "https://cdn.example/thumb.png",
                    PreviewUrl: "https://cdn.example/preview.png"
                )
            );
    }

    /// <summary>
    /// -1 means unlimited and MUST read as available — the invariant in
    /// <c>ItemAvailability.IsAvailable</c> that the contract surfaces.
    /// </summary>
    [Fact]
    public void Treats_unlimited_quantity_as_available()
    {
        var item = StoreBuilder.Item(Guid.NewGuid(), quantity: new ItemQuantity(-1, -1));

        var response = (StoreItemDetailResponse)item;

        response.QuantityCurrent.Should().Be(-1);
        response.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Treats_a_sold_out_item_as_unavailable()
    {
        var item = StoreBuilder.Item(Guid.NewGuid(), quantity: new ItemQuantity(10, 0));

        var response = (StoreItemDetailResponse)item;

        response.QuantityCurrent.Should().Be(0);
        response.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void Treats_a_disabled_item_as_unavailable()
    {
        var item = StoreBuilder.Item(Guid.NewGuid(), enabled: false);

        var response = (StoreItemDetailResponse)item;

        response.Enabled.Should().BeFalse();
        response.IsAvailable.Should().BeFalse();
    }

    /// <summary>
    /// WishlistEntry is an EF navigation with a private setter, so a unit test can only observe
    /// the unloaded case. The populated one is covered by IntegrationTests/Stores.
    /// </summary>
    [Fact]
    public void Maps_a_null_wishlist_entry_when_the_navigation_is_not_loaded()
    {
        var response = (StoreItemDetailResponse)StoreBuilder.Item(Guid.NewGuid());

        response.WishlistEntryId.Should().BeNull();
    }

    [Fact]
    public void Maps_missing_image_urls_to_null()
    {
        var response = (StoreItemDetailResponse)StoreBuilder.Item(Guid.NewGuid());

        response.ThumbnailUrl.Should().BeNull();
        response.PreviewUrl.Should().BeNull();
    }
}
