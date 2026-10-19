using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Wishlist;

/// <summary>
/// Narrowing and ordering the wishlist in the database. Filtering evaluates against entries; the
/// blocked filter reads the persisted flag, because a filter cannot be evaluated in SQL against a
/// value the database does not hold.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class WishlistSearchTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private Guid _alphaStoreId;
    private Guid _betaStoreId;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Filters

    [Fact]
    public async Task Name_search_matches_a_case_insensitive_substring()
    {
        var entries = await ListAsync(Filter() with { SearchTerm = "EMO" });

        entries.Should().NotBeEmpty();
        entries
            .Should()
            .OnlyContain(entry =>
                entry.StoreItem!.Name.Contains("emo", StringComparison.OrdinalIgnoreCase)
            );
    }

    [Fact]
    public async Task Availability_narrows_to_available_items()
    {
        var entries = await ListAsync(Filter() with { AvailableOnly = true });

        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(entry => entry.StoreItem!.Availability.IsAvailable);
        entries.Should().NotContain(entry => entry.StoreItem!.Name == "Sold Out Skin");
    }

    [Fact]
    public async Task Auto_redeem_narrows_to_auto_redeeming_entries()
    {
        var entries = await ListAsync(Filter() with { AutoRedeem = true });

        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(entry => entry.AutoRedeem);
    }

    [Fact]
    public async Task Notify_narrows_to_notifying_entries()
    {
        var entries = await ListAsync(Filter() with { NotifyOnAvailable = false });

        entries.Should().OnlyContain(entry => !entry.NotifyOnAvailable);
    }

    [Fact]
    public async Task Blocked_filter_selects_entries_flagged_as_blocked()
    {
        var entries = await ListAsync(Filter() with { BlockedOnInput = true });

        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(entry => entry.IsBlockedOnInput);
    }

    [Fact]
    public async Task Store_filter_narrows_to_one_stores_entries()
    {
        var entries = await ListAsync(Filter() with { StoreId = _betaStoreId });

        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(entry => entry.StoreItem!.WatchedStoreId == _betaStoreId);
    }

    [Fact]
    public async Task Filters_combine_conjunctively()
    {
        var entries = await ListAsync(Filter() with { AvailableOnly = true, AutoRedeem = true });

        entries
            .Should()
            .OnlyContain(entry => entry.AutoRedeem && entry.StoreItem!.Availability.IsAvailable);
    }

    [Fact]
    public async Task No_filter_returns_every_entry_the_caller_owns()
    {
        var entries = await ListAsync(Filter());

        entries.Should().HaveCount(5);
    }

    [Fact]
    public async Task Another_owners_entries_are_never_returned()
    {
        var entries = await ListAsync(Filter());

        entries.Should().OnlyContain(entry => entry.UserId == _userId);
    }

    #endregion

    #region Ordering

    /// <summary>
    /// Store name leads the ordering so the caller can group by walking the sequence. Without it,
    /// grouping would have to re-sort what the database already ordered.
    /// </summary>
    [Fact]
    public async Task Entries_arrive_grouped_by_store_display_name()
    {
        var entries = await ListAsync(Filter());

        var storeNames = entries
            .Select(entry => entry.StoreItem!.WatchedStore!.DisplayName)
            .ToList();

        storeNames.Should().BeInAscendingOrder();

        // Each store is one contiguous run, which is what lets the caller group by walking the
        // sequence: as many runs as there are distinct stores means no store is split in two.
        var runs = storeNames
            .Where((name, index) => index == 0 || storeNames[index - 1] != name)
            .Count();
        runs.Should().Be(storeNames.Distinct().Count());
    }

    [Fact]
    public async Task Entries_are_ordered_by_item_name_within_a_store()
    {
        var entries = await ListAsync(
            Filter() with
            {
                StoreId = _alphaStoreId,
                SortField = WishlistEntrySortField.ItemName,
            }
        );

        entries
            .Select(entry => entry.StoreItem!.Name.ToLowerInvariant())
            .Should()
            .BeInAscendingOrder();
    }

    [Fact]
    public async Task Entries_are_ordered_by_item_cost_when_asked()
    {
        var entries = await ListAsync(
            Filter() with
            {
                StoreId = _alphaStoreId,
                SortField = WishlistEntrySortField.ItemCost,
                SortDirection = SortDirection.Desc,
            }
        );

        entries.Select(entry => entry.StoreItem!.Cost).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Ordering_is_repeatable_for_tied_keys()
    {
        var filter = Filter() with { SortField = WishlistEntrySortField.ItemCost };

        var first = await ListAsync(filter);
        var second = await ListAsync(filter);

        first.Select(entry => entry.Id).Should().Equal(second.Select(entry => entry.Id));
    }

    #endregion

    #region The persisted flag

    /// <summary>
    /// The read trusts the stored flag, so the flag has to stay right. This drives a real coverage
    /// loss and a real restore through the domain and checks the flag matches a fresh computation
    /// at each step — that agreement is the safeguard the trade-off rests on.
    /// </summary>
    [Fact]
    public async Task The_persisted_flag_tracks_coverage_loss_and_restore()
    {
        var entryId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        await using (var scope = CreateScope())
        {
            var store = new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch-coverage"),
                "Coverage",
                _userId,
                Now
            );
            var item = Item(
                storeId,
                "ch-coverage",
                "Needs Email",
                100,
                current: 5,
                inputs: ["Email"]
            );
            store.AddItem(item);
            scope.Db.WatchedStores.Add(store);
            scope.Db.WishlistEntries.Add(new WishlistEntry(entryId, _userId, item.Id, Now));
            await scope.Db.SaveChangesAsync();
        }

        // Auto-redeem on with no profile value for "Email" → coverage is missing.
        await MutateEntryAsync(entryId, entry => entry.EnableAutoRedeem(Now));
        await MutateEntryAsync(entryId, entry => entry.BlockOnMissingInput(Now));

        (await BlockedFlagAsync(entryId)).Should().BeTrue();
        (await ReportedBlockedAsync(entryId)).Should().BeTrue("the read reports the stored flag");

        // Coverage restored.
        await MutateEntryAsync(entryId, entry => entry.ClearInputBlock(Now));

        (await BlockedFlagAsync(entryId)).Should().BeFalse();
        (await ReportedBlockedAsync(entryId)).Should().BeFalse();
    }

    [Fact]
    public async Task The_blocked_filter_and_the_reported_state_agree()
    {
        var blocked = await ListAsync(Filter() with { BlockedOnInput = true });
        var all = await ListAsync(Filter());

        blocked
            .Select(entry => entry.Id)
            .Should()
            .BeEquivalentTo(all.Where(entry => entry.IsBlockedOnInput).Select(entry => entry.Id));
    }

    #endregion

    private static WishlistEntryFilter Filter() =>
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

    private async Task<IReadOnlyList<WishlistEntry>> ListAsync(WishlistEntryFilter filter)
    {
        await using var scope = CreateScope();
        return await new WishlistEntryRepository(scope.Db).ListByUserWithItemsAsync(
            _userId,
            filter
        );
    }

    private async Task<bool> BlockedFlagAsync(Guid entryId)
    {
        await using var scope = CreateScope();
        return await scope
            .Db.WishlistEntries.Where(entry => entry.Id == entryId)
            .Select(entry => entry.IsBlockedOnInput)
            .FirstAsync();
    }

    private async Task<bool> ReportedBlockedAsync(Guid entryId)
    {
        var entries = await ListAsync(Filter());
        return entries.Single(entry => entry.Id == entryId).IsBlockedOnInput;
    }

    private async Task MutateEntryAsync(Guid entryId, Action<WishlistEntry> mutate)
    {
        await using var scope = CreateScope();
        var entry = await scope.Db.WishlistEntries.AsTracking().FirstAsync(e => e.Id == entryId);
        mutate(entry);
        await scope.Db.SaveChangesAsync();
    }

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        _alphaStoreId = Guid.NewGuid();
        _betaStoreId = Guid.NewGuid();

        var alpha = new WatchedStore(
            _alphaStoreId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-alpha"),
            "Alpha",
            _userId,
            Now
        );
        var emote = Item(_alphaStoreId, "ch-alpha", "Emote Pack", 100, current: 5);
        var badge = Item(_alphaStoreId, "ch-alpha", "Badge", 300, current: -1);
        var soldOut = Item(_alphaStoreId, "ch-alpha", "Sold Out Skin", 200, current: 0);
        alpha.AddItem(emote);
        alpha.AddItem(badge);
        alpha.AddItem(soldOut);

        var beta = new WatchedStore(
            _betaStoreId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-beta"),
            "Beta",
            _userId,
            Now
        );
        var emoji = Item(_betaStoreId, "ch-beta", "Emoji Bundle", 150, current: 2);
        var cape = Item(_betaStoreId, "ch-beta", "Cape", 400, current: 1);
        beta.AddItem(emoji);
        beta.AddItem(cape);

        var other = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-other"),
            "Other",
            _otherUserId,
            Now
        );
        var otherItem = Item(other.Id, "ch-other", "Someone Elses Emote", 100, current: 5);
        other.AddItem(otherItem);

        scope.Db.WatchedStores.AddRange(alpha, beta, other);

        var autoRedeeming = new WishlistEntry(Guid.NewGuid(), _userId, emote.Id, Now);
        autoRedeeming.EnableAutoRedeem(Now);

        var blockedEntry = new WishlistEntry(Guid.NewGuid(), _userId, badge.Id, Now);
        blockedEntry.BlockOnMissingInput(Now);

        var quiet = new WishlistEntry(Guid.NewGuid(), _userId, soldOut.Id, Now);
        quiet.SetNotifyOnAvailable(false, Now);

        scope.Db.WishlistEntries.AddRange(
            autoRedeeming,
            blockedEntry,
            quiet,
            new WishlistEntry(Guid.NewGuid(), _userId, emoji.Id, Now),
            new WishlistEntry(Guid.NewGuid(), _userId, cape.Id, Now),
            new WishlistEntry(Guid.NewGuid(), _otherUserId, otherItem.Id, Now)
        );

        await scope.Db.SaveChangesAsync();
    }

    private static StoreItem Item(
        Guid storeId,
        string channelId,
        string name,
        long cost,
        int current,
        IReadOnlyList<string>? inputs = null
    ) =>
        new(
            Guid.NewGuid(),
            storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId($"se-{Guid.NewGuid():N}"),
                new StoreExternalId(channelId),
                name,
                null,
                "perk",
                cost,
                "General",
                true,
                false,
                true,
                false,
                false,
                inputs ?? [],
                [],
                new ItemQuantity(10, current),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );

    private DbScope CreateScope() => new(fixture.ConnectionString);

    private sealed class DbScope : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public DbScope(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
            _provider = services.BuildServiceProvider();
            Db = _provider.GetRequiredService<AppDbContext>();
        }

        public AppDbContext Db { get; }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }
}
