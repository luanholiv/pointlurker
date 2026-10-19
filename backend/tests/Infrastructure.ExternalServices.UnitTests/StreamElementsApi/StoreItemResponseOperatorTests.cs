using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

public sealed class StoreItemResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set()
    {
        var dto = new StoreItemResponse
        {
            Id = "se-1",
            Channel = "ch-1",
            Name = "Item",
            Description = "Desc",
            Type = "perk",
            Cost = 250,
            CategoryName = "tier-2",
            Enabled = true,
            Featured = true,
            Public = true,
            SubscriberOnly = true,
            AllowMessages = true,
            UserInput = ["email"],
            Sources = ["pro"],
            Quantity = new QuantityResponse { Total = 10, Current = 7 },
            Cooldown = new CooldownResponse
            {
                User = 1,
                Global = 2,
                Category = 3,
            },
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Thumbnail = "https://cdn.example.com/thumb.png",
            Preview = "https://cdn.example.com/preview.png",
        };

        var item = (ProviderStoreItem)dto;

        item.Should()
            .BeEquivalentTo(
                new ProviderStoreItem(
                    Provider: StoreProvider.StreamElements,
                    Id: new ProviderItemId("se-1"),
                    Store: new StoreExternalId("ch-1"),
                    Name: "Item",
                    Description: "Desc",
                    Type: "perk",
                    Cost: 250,
                    CategoryName: "tier-2",
                    Enabled: true,
                    Featured: true,
                    Public: true,
                    SubscriberOnly: true,
                    AllowMessages: true,
                    UserInputDefinitions: ["email"],
                    Sources: ["pro"],
                    Quantity: new ItemQuantity(10, 7),
                    Cooldown: new ItemCooldown(1, 2, 3),
                    CreatedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    UpdatedAt: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                    ThumbnailUrl: "https://cdn.example.com/thumb.png",
                    PreviewUrl: "https://cdn.example.com/preview.png"
                )
            );
    }

    [Fact]
    public void Maps_missing_image_urls_to_null()
    {
        var dto = MinimalDto();

        var item = (ProviderStoreItem)dto;

        item.ThumbnailUrl.Should().BeNull();
        item.PreviewUrl.Should().BeNull();
    }

    [Fact]
    public void Maps_unlimited_quantity_as_minus_one()
    {
        var dto = MinimalDto() with
        {
            Quantity = new QuantityResponse { Total = -1, Current = -1 },
        };

        var item = (ProviderStoreItem)dto;

        item.Quantity.Total.Should().Be(-1);
        item.Quantity.Current.Should().Be(-1);
    }

    [Fact]
    public void Maps_missing_optional_fields_to_defaults()
    {
        var dto = MinimalDto();

        var item = (ProviderStoreItem)dto;

        item.Description.Should().BeNull();
        item.UpdatedAt.Should().BeNull();
        item.CategoryName.Should().BeEmpty();
        item.UserInputDefinitions.Should().BeEmpty();
        item.Sources.Should().BeEmpty();
    }

    [Fact]
    public void Maps_empty_user_inputs()
    {
        var dto = MinimalDto() with { UserInput = [] };

        var item = (ProviderStoreItem)dto;

        item.UserInputDefinitions.Should().BeEmpty();
    }

    private static StoreItemResponse MinimalDto() =>
        new()
        {
            Id = "se-1",
            Channel = "ch-1",
            Name = "Item",
            Type = "perk",
            Cost = 100,
            Enabled = true,
            Quantity = new QuantityResponse { Total = 0, Current = 0 },
            Cooldown = new CooldownResponse(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
