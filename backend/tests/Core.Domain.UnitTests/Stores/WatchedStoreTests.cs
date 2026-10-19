using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class WatchedStoreTests
{
    [Fact]
    public void Activate_sets_IsActive_to_true()
    {
        var store = CreateStore();
        store.Deactivate();

        store.Activate();

        store.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_then_Activate_round_trips_correctly()
    {
        var store = CreateStore();
        store.IsActive.Should().BeTrue();

        store.Deactivate();
        store.IsActive.Should().BeFalse();

        store.Activate();
        store.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_sets_IsActive_to_false()
    {
        var store = CreateStore();

        store.Deactivate();

        store.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Constructor_without_avatar_leaves_AvatarUrl_null()
    {
        var store = CreateStore();

        store.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public void Constructor_accepts_avatar_url()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("test-channel"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            avatarUrl: "https://cdn.example.com/avatar.png"
        );

        store.AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");
    }

    [Fact]
    public void Constructor_normalizes_blank_avatar_to_null()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("test-channel"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            avatarUrl: "   "
        );

        store.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public void UpdateChannelInfo_updates_display_name_and_avatar()
    {
        var store = CreateStore();

        store.UpdateChannelInfo("New Name", "https://cdn.example.com/new.png");

        store.DisplayName.Should().Be("New Name");
        store.AvatarUrl.Should().Be("https://cdn.example.com/new.png");
    }

    [Fact]
    public void UpdateChannelInfo_trims_and_normalizes_blank_avatar_to_null()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("test-channel"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            avatarUrl: "https://cdn.example.com/old.png"
        );

        store.UpdateChannelInfo("Test Store", "   ");

        store.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public void UpdateChannelInfo_ignores_blank_display_name_keeping_invariant()
    {
        var store = CreateStore();

        store.UpdateChannelInfo("   ", "https://cdn.example.com/new.png");

        store.DisplayName.Should().Be("Test Store");
        store.AvatarUrl.Should().Be("https://cdn.example.com/new.png");
    }

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("test-channel"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
