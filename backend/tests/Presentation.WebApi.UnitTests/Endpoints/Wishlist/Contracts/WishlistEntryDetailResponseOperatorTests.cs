using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Contracts;

public sealed class WishlistEntryDetailResponseOperatorTests
{
    private static readonly DateTimeOffset Created = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_full_field_set()
    {
        var entry = new GetWishlistEntryResult(
            Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            StoreItemId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ItemName: "Shoutout",
            ItemCost: 500,
            IsAvailable: true,
            StoreName: "xQc Store",
            StoreId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            NotifyOnAvailable: true,
            AutoRedeem: true,
            DesiredRedemptions: 1,
            QuotaUsed: 0,
            AllowMessages: true,
            RedemptionMessage: "gg",
            IsBlocked: true,
            MissingLabels: ["Discord tag"],
            CreatedAt: Created,
            UpdatedAt: Created.AddDays(1)
        );

        var response = (WishlistEntryDetailResponse)entry;

        response
            .Should()
            .BeEquivalentTo(
                new WishlistEntryDetailResponse(
                    Id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    StoreItemId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    ItemName: "Shoutout",
                    ItemCost: 500,
                    IsAvailable: true,
                    StoreName: "xQc Store",
                    StoreId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    NotifyOnAvailable: true,
                    AutoRedeem: true,
                    DesiredRedemptions: 1,
                    QuotaUsed: 0,
                    AllowMessages: true,
                    RedemptionMessage: "gg",
                    IsBlocked: true,
                    MissingLabels: ["Discord tag"],
                    CreatedAt: Created,
                    UpdatedAt: Created.AddDays(1)
                )
            );
    }

    [Fact]
    public void Maps_an_unblocked_entry_with_no_message()
    {
        var response = (WishlistEntryDetailResponse)MinimalResult();

        response.RedemptionMessage.Should().BeNull();
        response.IsBlocked.Should().BeFalse();
        response.MissingLabels.Should().BeEmpty();
    }

    private static GetWishlistEntryResult MinimalResult() =>
        new(
            Id: Guid.NewGuid(),
            StoreItemId: Guid.NewGuid(),
            ItemName: "Shoutout",
            ItemCost: 500,
            IsAvailable: false,
            StoreName: "xQc Store",
            StoreId: Guid.NewGuid(),
            NotifyOnAvailable: false,
            AutoRedeem: false,
            DesiredRedemptions: 1,
            QuotaUsed: 0,
            AllowMessages: false,
            RedemptionMessage: null,
            IsBlocked: false,
            MissingLabels: [],
            CreatedAt: Created,
            UpdatedAt: Created
        );
}
