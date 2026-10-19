using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Contracts;

public sealed class WishlistStoreGroupResponseOperatorTests
{
    private static readonly DateTimeOffset Synced = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_full_field_set_and_converts_nested_entries()
    {
        var group = new WishlistStoreGroup(
            StoreId: Guid.Parse("77777777-7777-7777-7777-777777777777"),
            StoreName: "xQc Store",
            IsActive: true,
            LastSyncedAt: Synced,
            Entries: [Entry("First"), Entry("Second")],
            AvatarUrl: "https://cdn.example/avatar.png"
        );

        var response = (WishlistStoreGroupResponse)group;

        response.StoreId.Should().Be(Guid.Parse("77777777-7777-7777-7777-777777777777"));
        response.StoreName.Should().Be("xQc Store");
        response.IsActive.Should().BeTrue();
        response.LastSyncedAt.Should().Be(Synced);
        response.AvatarUrl.Should().Be("https://cdn.example/avatar.png");
        response.Entries.Select(e => e.ItemName).Should().Equal("First", "Second");
    }

    [Fact]
    public void Maps_a_never_synced_store_with_no_avatar_and_no_entries()
    {
        var group = new WishlistStoreGroup(
            StoreId: Guid.NewGuid(),
            StoreName: "Fresh Store",
            IsActive: false,
            LastSyncedAt: null,
            Entries: []
        );

        var response = (WishlistStoreGroupResponse)group;

        response.LastSyncedAt.Should().BeNull();
        response.AvatarUrl.Should().BeNull();
        response.Entries.Should().BeEmpty();
    }

    private static WishlistEntryGroupItem Entry(string itemName) =>
        new(
            Id: Guid.NewGuid(),
            StoreItemId: Guid.NewGuid(),
            ItemName: itemName,
            ItemCost: 100,
            IsAvailable: true,
            NotifyOnAvailable: false,
            AutoRedeem: false,
            DesiredRedemptions: 1,
            QuotaUsed: 0,
            IsBlocked: false,
            MissingLabels: [],
            CreatedAt: Synced
        );
}
