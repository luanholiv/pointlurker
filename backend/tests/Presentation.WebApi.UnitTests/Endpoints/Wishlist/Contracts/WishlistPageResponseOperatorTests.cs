using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Contracts;

public sealed class WishlistPageResponseOperatorTests
{
    [Fact]
    public void Converts_every_store_group_and_preserves_order()
    {
        var grouped = Page(Group("Alpha"), Group("Beta"));

        var response = (WishlistPageResponse)grouped;

        response.Items.Select(s => s.StoreName).Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void Maps_an_empty_wishlist()
    {
        var response = (WishlistPageResponse)Page();

        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    /// <summary>
    /// A row is a store group, so the store's fields are carried once per group rather than copied
    /// onto every entry. That is the whole reason the envelope constrains only the envelope.
    /// </summary>
    [Fact]
    public void Carries_store_fields_once_per_group_not_per_entry()
    {
        var response = (WishlistPageResponse)Page(Group("Alpha"));

        var group = response.Items.Should().ContainSingle().Subject;
        group.StoreName.Should().Be("Alpha");
        typeof(WishlistEntryListItemResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(["StoreName", "StoreId", "AvatarUrl"]);
    }

    [Fact]
    public void Declares_exactly_the_shared_envelope()
    {
        typeof(WishlistPageResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("Items", "TotalCount", "Offset", "Limit");
    }

    private static WishlistGroupedResponse Page(params WishlistStoreGroup[] groups) =>
        new(groups, groups.Length, Offset: 0, Limit: groups.Length);

    private static WishlistStoreGroup Group(string storeName) =>
        new(
            StoreId: Guid.NewGuid(),
            StoreName: storeName,
            IsActive: true,
            LastSyncedAt: null,
            Entries: []
        );
}
