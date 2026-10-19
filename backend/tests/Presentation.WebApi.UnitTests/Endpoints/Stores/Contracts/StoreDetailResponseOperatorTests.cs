using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Contracts;

public sealed class StoreDetailResponseOperatorTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_the_full_envelope_from_a_projected_row()
    {
        var row = new WatchedStoreDetailRow(
            Guid.NewGuid(),
            "channel-1",
            "xQc Store",
            IsActive: true,
            LastSyncedAt: Created.AddDays(1),
            CreatedAt: Created,
            ItemCount: 2,
            AvatarUrl: "https://cdn.example/avatar.png",
            StoreProvider.StreamElements
        );

        var response = (StoreDetailResponse)row;

        response.Id.Should().Be(row.Id);
        response.ChannelId.Should().Be("channel-1");
        response.DisplayName.Should().Be("xQc Store");
        response.IsActive.Should().BeTrue();
        response.LastSyncedAt.Should().Be(Created.AddDays(1));
        response.CreatedAt.Should().Be(Created);
        response.AvatarUrl.Should().Be("https://cdn.example/avatar.png");
        response.ItemCount.Should().Be(2);
    }

    /// <summary>
    /// Items moved to their own filtered, paged endpoint. What stays is the count, because the page
    /// still has to distinguish an empty store from an over-filtered one.
    /// </summary>
    [Fact]
    public void Carries_a_count_rather_than_the_item_collection()
    {
        typeof(StoreDetailResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain("Items")
            .And.Contain("ItemCount");
    }

    /// <summary>
    /// Missing labels are computed per request, not stored, so reading a store on its own must
    /// report none rather than guessing.
    /// </summary>
    [Fact]
    public void Reports_no_missing_labels_when_converting_a_bare_store()
    {
        var response = (StoreDetailResponse)Row();

        response.MissingLabels.Should().BeEmpty();
    }

    [Fact]
    public void Layers_missing_labels_on_when_converting_the_add_store_result()
    {
        var store = StoreBuilder.Store();
        store.AddItem(StoreBuilder.Item(store.Id, "Shoutout"));
        var result = new AddWatchedStoreResult(store, ["Discord tag", "Nick (IGN)"]);

        var response = (StoreDetailResponse)result;

        response.MissingLabels.Should().Equal("Discord tag", "Nick (IGN)");
        response.Id.Should().Be(store.Id);
        // The add path holds the aggregate it just built, so it counts what it inserted.
        response.ItemCount.Should().Be(1);
    }

    [Fact]
    public void Maps_a_never_synced_store_with_no_items_and_no_avatar()
    {
        var response = (StoreDetailResponse)Row();

        response.LastSyncedAt.Should().BeNull();
        response.AvatarUrl.Should().BeNull();
        response.ItemCount.Should().Be(0);
    }

    [Fact]
    public void Maps_a_deactivated_store()
    {
        var response = (StoreDetailResponse)(Row() with { IsActive = false });

        response.IsActive.Should().BeFalse();
    }

    private static WatchedStoreDetailRow Row() =>
        new(
            Guid.NewGuid(),
            "channel-1",
            "xQc Store",
            IsActive: true,
            LastSyncedAt: null,
            CreatedAt: Created,
            ItemCount: 0,
            AvatarUrl: null,
            StoreProvider.StreamElements
        );
}
