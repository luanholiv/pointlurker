using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class ListWishlistEntriesGroupedTests
{
    private readonly IWishlistEntryRepository _wishlistEntryRepository =
        Substitute.For<IWishlistEntryRepository>();
    private readonly IRedemptionRepository _redemptionRepository =
        Substitute.For<IRedemptionRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly ListWishlistEntriesGrouped _sut;

    public ListWishlistEntriesGroupedTests()
    {
        _redemptionRepository
            .CountHoldingQuotaAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Dictionary<Guid, RedemptionQuotaUsage>());

        _sut = new ListWishlistEntriesGrouped(
            _wishlistEntryRepository,
            _redemptionRepository,
            _resolver
        );
    }

    [Fact]
    public async Task ExecuteAsync_returns_grouped_entries_by_store()
    {
        var userId = Guid.NewGuid();
        var store1 = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store 1",
            userId,
            Now
        );
        var store2 = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch2"),
            "Store 2",
            userId,
            Now
        );

        var entry1 = CreateEntry(userId, store1);
        var entry2 = CreateEntry(userId, store2);

        StubEntries(userId, entry1, entry2);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result
            .Value.Items.Select(group => group.StoreName)
            .Should()
            .BeEquivalentTo(["Store 1", "Store 2"]);
    }

    [Fact]
    public async Task ExecuteAsync_groups_one_store_once_even_with_separate_store_instances()
    {
        // The read does not track, so EF performs no identity resolution: two entries in the same
        // store arrive holding equal-but-distinct WatchedStore instances. Grouping by reference would
        // report the store twice.
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var entry1 = CreateEntry(
            userId,
            new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch1"),
                "Store",
                userId,
                Now
            )
        );
        var entry2 = CreateEntry(
            userId,
            new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch1"),
                "Store",
                userId,
                Now
            )
        );

        StubEntries(userId, entry1, entry2);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Entries.Should().HaveCount(2);
    }

    /// <summary>
    /// Blocked state is the persisted flag, not a recompute. Filtering by it has to happen in SQL,
    /// and SQL can only filter on what is stored — reporting a freshly computed value here would let
    /// the filter and the field disagree. That the flag itself stays correct across coverage changes
    /// is a separate property, covered by an integration test over the real transitions.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_reports_the_persisted_blocked_flag()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            userId,
            Now
        );
        var entry = CreateEntry(
            userId,
            store,
            userInputDefinitions: ["email", "name", "phone"],
            autoRedeem: true
        );
        entry.BlockOnMissingInput(Now);

        StubEntries(userId, entry);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].Entries[0].IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_reports_not_blocked_when_the_flag_is_clear()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            userId,
            Now
        );
        var entry = CreateEntry(userId, store, userInputDefinitions: ["email", "name"]);

        StubEntries(userId, entry);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].Entries[0].IsBlocked.Should().BeFalse();
    }

    /// <summary>
    /// Missing labels stay a computed projection over the entries actually returned — they are a
    /// display field, not a predicate, so they are still derived rather than stored.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_still_computes_missing_labels_for_returned_entries()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            userId,
            Now
        );
        var entry = CreateEntry(
            userId,
            store,
            userInputDefinitions: ["email", "name", "phone"],
            autoRedeem: true
        );

        StubEntries(userId, entry);

        var result = await ExecuteAsync(userId);

        result
            .Value.Items[0]
            .Entries[0]
            .MissingLabels.Should()
            .BeEquivalentTo(["email", "name", "phone"]);
    }

    [Fact]
    public async Task ExecuteAsync_reports_no_missing_labels_when_the_profile_covers_them()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            userId,
            Now
        );
        store.UpsertValue("email", "ct1", Now);
        store.UpsertValue("name", "ct2", Now);

        var entry = CreateEntry(
            userId,
            store,
            userInputDefinitions: ["email", "name"],
            autoRedeem: true
        );

        StubEntries(userId, entry);

        var result = await ExecuteAsync(userId);

        result.Value.Items[0].Entries[0].MissingLabels.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_passes_the_filter_through_to_the_repository()
    {
        var userId = Guid.NewGuid();
        StubEntries(userId);

        await _sut.ExecuteAsync(
            userId,
            UnfilteredWishlist with
            {
                SearchTerm = "emote",
                AvailableOnly = true,
            }
        );

        await _wishlistEntryRepository
            .Received(1)
            .ListByUserWithItemsAsync(
                userId,
                Arg.Is<WishlistEntryFilter>(filter =>
                    filter.SearchTerm == "emote" && filter.AvailableOnly == true
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_reports_the_number_of_store_rows_as_the_total()
    {
        var userId = Guid.NewGuid();
        var one = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "One",
            userId,
            Now
        );
        var two = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch2"),
            "Two",
            userId,
            Now
        );

        StubEntries(userId, CreateEntry(userId, one), CreateEntry(userId, two));

        var result = await ExecuteAsync(userId);

        // A row is a store group, so the total counts stores retaining an entry — not leaf entries.
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_includes_store_avatar_and_item_thumbnail()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            userId,
            Now,
            avatarUrl: "https://cdn.example.com/avatar.png"
        );
        var entry = CreateEntry(userId, store, thumbnailUrl: "https://cdn.example.com/thumb.png");

        StubEntries(userId, entry);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");
        result
            .Value.Items[0]
            .Entries[0]
            .ThumbnailUrl.Should()
            .Be("https://cdn.example.com/thumb.png");
    }

    [Fact]
    public async Task ExecuteAsync_returns_empty_stores_when_no_entries()
    {
        var userId = Guid.NewGuid();

        StubEntries(userId);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_skips_an_entry_whose_item_graph_did_not_load()
    {
        var userId = Guid.NewGuid();
        var orphan = new WishlistEntry(Guid.NewGuid(), userId, Guid.NewGuid(), Now);

        StubEntries(userId, orphan);

        var result = await ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_reports_the_quota_used_against_the_desired_count()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store 1",
            userId,
            Now
        );
        var entry = CreateEntry(userId, store, autoRedeem: true);
        entry.SetDesiredRedemptions(3, Now);
        StubEntries(userId, entry);

        _redemptionRepository
            .CountHoldingQuotaAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new Dictionary<Guid, RedemptionQuotaUsage>
                {
                    [entry.Id] = new(QuotaUsed: 2, Inflight: 0),
                }
            );

        var result = await ExecuteAsync(userId);

        var item = result.Value.Items[0].Entries[0];
        item.DesiredRedemptions.Should().Be(3);
        item.QuotaUsed.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_reports_no_quota_used_for_an_entry_never_redeemed()
    {
        var userId = Guid.NewGuid();
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store 1",
            userId,
            Now
        );
        StubEntries(userId, CreateEntry(userId, store));

        var result = await ExecuteAsync(userId);

        result.Value.Items[0].Entries[0].QuotaUsed.Should().Be(0);
    }

    private Task<Result<WishlistGroupedResponse, WishlistError>> ExecuteAsync(Guid userId) =>
        _sut.ExecuteAsync(userId, UnfilteredWishlist);

    private static WishlistEntryFilter UnfilteredWishlist =>
        new(
            SearchTerm: null,
            AvailableOnly: null,
            AutoRedeem: null,
            NotifyOnAvailable: null,
            BlockedOnInput: null,
            StoreId: null,
            SortField: WishlistEntrySortField.ItemName,
            SortDirection: SortDirection.Asc
        );

    private void StubEntries(Guid userId, params WishlistEntry[] entries) =>
        _wishlistEntryRepository
            .ListByUserWithItemsAsync(
                userId,
                Arg.Any<WishlistEntryFilter>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(entries);

    /// <summary>
    /// A wishlist entry carrying the item → store graph the read eager-loads. The navigations have
    /// private setters that only EF fills, so they are set the way the sync tests set them.
    /// </summary>
    private static WishlistEntry CreateEntry(
        Guid userId,
        WatchedStore store,
        IReadOnlyList<string>? userInputDefinitions = null,
        string? thumbnailUrl = null,
        bool autoRedeem = false
    )
    {
        var item = CreateStoreItem(store.Id, userInputDefinitions, thumbnailUrl);
        typeof(StoreItem).GetProperty(nameof(StoreItem.WatchedStore))!.SetValue(item, store);

        var entry = new WishlistEntry(Guid.NewGuid(), userId, item.Id, Now);
        if (autoRedeem)
        {
            entry.EnableAutoRedeem(Now);
        }
        typeof(WishlistEntry).GetProperty(nameof(WishlistEntry.StoreItem))!.SetValue(entry, item);

        return entry;
    }

    private static StoreItem CreateStoreItem(
        Guid watchedStoreId,
        IReadOnlyList<string>? userInputDefinitions = null,
        string? thumbnailUrl = null
    )
    {
        var data = new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId($"se-{Guid.NewGuid():N}"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            true,
            false,
            true,
            false,
            false,
            userInputDefinitions ?? [],
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now,
            thumbnailUrl
        );

        return new StoreItem(Guid.NewGuid(), watchedStoreId, data);
    }
}
