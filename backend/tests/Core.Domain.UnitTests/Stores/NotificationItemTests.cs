using FluentAssertions;
using PointLurker.Core.Domain.Stores;

namespace Core.Domain.UnitTests.Stores;

public sealed class NotificationItemTests
{
    [Fact]
    public void Constructor_stores_all_fields()
    {
        var id = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var storeItemId = Guid.NewGuid();

        var item = new NotificationItem(id, notificationId, storeItemId, "xQc", "VIP Badge");

        item.Id.Should().Be(id);
        item.NotificationId.Should().Be(notificationId);
        item.StoreItemId.Should().Be(storeItemId);
        item.StoreName.Should().Be("xQc");
        item.ItemName.Should().Be("VIP Badge");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Constructor_rejects_empty_store_name(string? storeName)
    {
        var act = () =>
            new NotificationItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                storeName!,
                "Item"
            );
        act.Should().Throw<ArgumentException>().WithParameterName("storeName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Constructor_rejects_empty_item_name(string? itemName)
    {
        var act = () =>
            new NotificationItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Store",
                itemName!
            );
        act.Should().Throw<ArgumentException>().WithParameterName("itemName");
    }
}
