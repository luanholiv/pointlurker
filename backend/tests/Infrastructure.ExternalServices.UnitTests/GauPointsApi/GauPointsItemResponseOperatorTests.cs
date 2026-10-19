using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;

namespace Infrastructure.ExternalServices.UnitTests.GauPointsApi;

public sealed class GauPointsItemResponseOperatorTests
{
    private static GauPointsItemResponse Item(string type = "digital", int? stock = 9) =>
        new()
        {
            Id = "netflix",
            Name = "Gift Card Netflix",
            Description = "R$ 35,00",
            Cost = 8000,
            Type = type,
            Stock = stock,
            Image = "https://cdn.example.com/netflix.png",
        };

    [Fact]
    public void Maps_onto_the_one_GauPoints_store()
    {
        var item = (ProviderStoreItem)Item();

        item.Provider.Should().Be(StoreProvider.GauPoints);
        item.Id.Should().Be(new ProviderItemId("netflix"));
        item.Store.Should().Be(new StoreExternalId("gaupoints"));
        item.Name.Should().Be("Gift Card Netflix");
        item.Description.Should().Be("R$ 35,00");
        item.Cost.Should().Be(8000);
        item.Type.Should().Be("digital");
        item.CategoryName.Should().Be("digital");
        item.ThumbnailUrl.Should().Be("https://cdn.example.com/netflix.png");
        item.PreviewUrl.Should().Be("https://cdn.example.com/netflix.png");
    }

    /// <summary>
    /// Stock with no total and no unlimited sentinel, always enabled: the domain's availability
    /// rule then reads as <c>stock &gt; 0</c> with no change to it.
    /// </summary>
    [Theory]
    [InlineData(9, true)]
    [InlineData(0, false)]
    public void Stock_is_both_total_and_current_and_drives_availability(int stock, bool available)
    {
        var item = (ProviderStoreItem)Item(stock: stock);

        item.Enabled.Should().BeTrue();
        item.Quantity.Should().Be(new ItemQuantity(stock, stock));
        (item.Quantity.Current > 0).Should().Be(available);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-3)]
    public void A_missing_or_negative_stock_reads_as_none_left(int? stock)
    {
        ((ProviderStoreItem)Item(stock: stock)).Quantity.Should().Be(new ItemQuantity(0, 0));
    }

    [Fact]
    public void Nothing_GauPoints_lacks_is_invented()
    {
        var item = (ProviderStoreItem)Item();

        item.AllowMessages.Should().BeFalse();
        item.SubscriberOnly.Should().BeFalse();
        item.Featured.Should().BeFalse();
        item.Sources.Should().BeEmpty();
        item.Cooldown.Should().Be(new ItemCooldown(0, 0, 0));
        item.CreatedAt.Should().BeNull();
        item.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void A_digital_item_asks_for_three_inputs_in_prose()
    {
        ((ProviderStoreItem)Item("digital"))
            .UserInputDefinitions.Should()
            .Equal("Full name", "Phone", "E-mail");
    }

    [Theory]
    [InlineData("physical")]
    [InlineData("something-new")]
    public void Anything_not_digital_also_asks_for_an_address(string type)
    {
        ((ProviderStoreItem)Item(type))
            .UserInputDefinitions.Should()
            .Equal("Full name", "Phone", "E-mail", "Address");
    }
}
