using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class ProviderStoreItemMappingTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);
    private static readonly DateTimeOffset FetchedAt = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    [Fact]
    public void Maps_every_field_onto_the_domain_api_data()
    {
        var item = CreateItem();

        var data = item.ToApiData(FetchedAt);

        data.Provider.Should().Be(StoreProvider.StreamElements);
        data.ProviderItemId.Value.Should().Be("se-item-1");
        data.StoreExternalId.Value.Should().Be("channel-1");
        data.Name.Should().Be("Shoutout");
        data.Description.Should().Be("A shoutout on stream");
        data.Type.Should().Be("perk");
        data.Cost.Should().Be(1500);
        data.CategoryName.Should().Be("perks");
        data.Enabled.Should().BeTrue();
        data.Featured.Should().BeTrue();
        data.Public.Should().BeTrue();
        data.SubscriberOnly.Should().BeTrue();
        data.AllowMessages.Should().BeTrue();
        data.UserInputDefinitions.Should().Equal("Discord Tag", "Nick (IGN)");
        data.Sources.Should().Equal("twitch", "youtube");
        data.Quantity.Should().Be(new ItemQuantity(10, 4));
        data.Cooldown.Should().Be(new ItemCooldown(60, 120, 180));
        data.ProviderCreatedAt.Should().Be(CreatedAt);
        data.ProviderUpdatedAt.Should().Be(UpdatedAt);
        data.ThumbnailUrl.Should().Be("https://cdn.test/thumb.png");
        data.PreviewUrl.Should().Be("https://cdn.test/preview.png");
    }

    [Fact]
    public void Carries_the_provider_through_rather_than_assuming_StreamElements()
    {
        var item = CreateItem() with { Provider = StoreProvider.GauPoints };

        var data = item.ToApiData(FetchedAt);

        data.Provider.Should().Be(StoreProvider.GauPoints);
    }

    /// <summary>
    /// The observation instant is supplied by the caller — one instant per fetched payload — rather
    /// than read from the clock inside the mapping, so every item of a sync batch shares it.
    /// </summary>
    [Fact]
    public void Stamps_the_supplied_instant_as_LastFetchedAt()
    {
        var item = CreateItem();

        var data = item.ToApiData(FetchedAt);

        data.LastFetchedAt.Should().Be(FetchedAt);
    }

    [Fact]
    public void Carries_the_optional_fields_through_as_null_when_absent()
    {
        var item = CreateItem() with
        {
            Description = null,
            CreatedAt = null,
            UpdatedAt = null,
            ThumbnailUrl = null,
            PreviewUrl = null,
        };

        var data = item.ToApiData(FetchedAt);

        data.Description.Should().BeNull();
        data.ProviderCreatedAt.Should().BeNull();
        data.ProviderUpdatedAt.Should().BeNull();
        data.ThumbnailUrl.Should().BeNull();
        data.PreviewUrl.Should().BeNull();
    }

    private static ProviderStoreItem CreateItem() =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId("se-item-1"),
            new StoreExternalId("channel-1"),
            "Shoutout",
            "A shoutout on stream",
            "perk",
            1500,
            "perks",
            true,
            true,
            true,
            true,
            true,
            ["Discord Tag", "Nick (IGN)"],
            ["twitch", "youtube"],
            new ItemQuantity(10, 4),
            new ItemCooldown(60, 120, 180),
            CreatedAt,
            UpdatedAt,
            "https://cdn.test/thumb.png",
            "https://cdn.test/preview.png"
        );
}
