using FluentAssertions;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Contracts;

public sealed class StoreListItemResponseOperatorTests
{
    private static readonly DateTimeOffset Synced = StoreBuilder.Created.AddDays(1);

    [Fact]
    public void Maps_full_field_set()
    {
        var row = Row() with { LastSyncedAt = Synced, ItemCount = 1 };

        var response = (StoreListItemResponse)row;

        response
            .Should()
            .BeEquivalentTo(
                new StoreListItemResponse(
                    Id: row.Id,
                    ChannelId: "channel-1",
                    DisplayName: "xQc Store",
                    IsActive: true,
                    LastSyncedAt: Synced,
                    ItemCount: 1,
                    AvatarUrl: "https://cdn.example/avatar.png",
                    Provider: "streamelements"
                )
            );
    }

    /// <summary>
    /// The list projection reports a count, not the items — a store with many items must not drag
    /// the whole collection onto the wire, and the count is now computed by the database rather
    /// than by loading the collection to measure it.
    /// </summary>
    [Fact]
    public void Reports_the_item_count_rather_than_the_items()
    {
        var response = (StoreListItemResponse)(Row() with { ItemCount = 3 });

        response.ItemCount.Should().Be(3);
        response.GetType().GetProperty("Items").Should().BeNull();
    }

    [Fact]
    public void Maps_a_never_synced_store_with_no_avatar()
    {
        var response = (StoreListItemResponse)(
            Row() with
            {
                LastSyncedAt = null,
                AvatarUrl = null,
            }
        );

        response.LastSyncedAt.Should().BeNull();
        response.AvatarUrl.Should().BeNull();
        response.ItemCount.Should().Be(0);
    }

    private static WatchedStoreListRow Row() =>
        new(
            Guid.NewGuid(),
            "channel-1",
            "xQc Store",
            IsActive: true,
            LastSyncedAt: null,
            ItemCount: 0,
            AvatarUrl: "https://cdn.example/avatar.png",
            StoreProvider.StreamElements
        );
}
