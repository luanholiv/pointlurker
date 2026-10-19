using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Contracts;

public sealed class WishlistEntryListItemResponseOperatorTests
{
    private static readonly DateTimeOffset Created = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_full_field_set()
    {
        var item = new WishlistEntryGroupItem(
            Id: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            StoreItemId: Guid.Parse("66666666-6666-6666-6666-666666666666"),
            ItemName: "Shoutout",
            ItemCost: 500,
            IsAvailable: true,
            NotifyOnAvailable: true,
            AutoRedeem: true,
            DesiredRedemptions: 1,
            QuotaUsed: 0,
            IsBlocked: true,
            MissingLabels: ["Discord tag", "Nick (IGN)"],
            CreatedAt: Created,
            ThumbnailUrl: "https://cdn.example/thumb.png"
        );

        var response = (WishlistEntryListItemResponse)item;

        response
            .Should()
            .BeEquivalentTo(
                new WishlistEntryListItemResponse(
                    Id: Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    StoreItemId: Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    ItemName: "Shoutout",
                    ItemCost: 500,
                    IsAvailable: true,
                    NotifyOnAvailable: true,
                    AutoRedeem: true,
                    DesiredRedemptions: 1,
                    QuotaUsed: 0,
                    IsBlocked: true,
                    MissingLabels: ["Discord tag", "Nick (IGN)"],
                    CreatedAt: Created,
                    ThumbnailUrl: "https://cdn.example/thumb.png"
                )
            );
    }

    [Fact]
    public void Maps_a_missing_thumbnail_to_null()
    {
        var response = (WishlistEntryListItemResponse)MinimalItem();

        response.ThumbnailUrl.Should().BeNull();
    }

    [Fact]
    public void Preserves_missing_label_order()
    {
        var item = MinimalItem() with { MissingLabels = ["b", "a", "c"] };

        var response = (WishlistEntryListItemResponse)item;

        response.MissingLabels.Should().Equal("b", "a", "c");
    }

    private static WishlistEntryGroupItem MinimalItem() =>
        new(
            Id: Guid.NewGuid(),
            StoreItemId: Guid.NewGuid(),
            ItemName: "Shoutout",
            ItemCost: 500,
            IsAvailable: false,
            NotifyOnAvailable: false,
            AutoRedeem: false,
            DesiredRedemptions: 1,
            QuotaUsed: 0,
            IsBlocked: false,
            MissingLabels: [],
            CreatedAt: Created
        );
}
