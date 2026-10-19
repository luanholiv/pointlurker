using FluentAssertions;
using PointLurker.Core.Domain.Stores;

namespace Core.Domain.UnitTests.Stores;

public sealed class StoreItemBecameUnavailableTests
{
    [Fact]
    public void Constructor_stores_all_fields()
    {
        var storeItemId = Guid.NewGuid();
        var watchedStoreId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var evt = new StoreItemBecameUnavailable(
            storeItemId,
            watchedStoreId,
            "Rare Skin",
            occurredAt
        );

        evt.StoreItemId.Should().Be(storeItemId);
        evt.WatchedStoreId.Should().Be(watchedStoreId);
        evt.ItemName.Should().Be("Rare Skin");
        evt.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var id = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var at = DateTimeOffset.UtcNow;

        var a = new StoreItemBecameUnavailable(id, storeId, "Item", at);
        var b = new StoreItemBecameUnavailable(id, storeId, "Item", at);

        a.Should().Be(b);
    }
}
