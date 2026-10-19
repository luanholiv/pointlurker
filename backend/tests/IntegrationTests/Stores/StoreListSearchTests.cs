using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Stores;

/// <summary>
/// Narrowing the watched-store list, and the active/paused totals counted across the whole
/// collection. The totals deliberately ignore the filter — that is what lets the list be narrowed
/// without the headline numbers moving underneath it.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class StoreListSearchTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Text_matches_the_display_name()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter("shroud", null));

        rows.Select(row => row.DisplayName).Should().Equal("Shroud Store");
    }

    /// <summary>
    /// "Identifier" is the store id, which is what the client filter this replaces matched
    /// (<c>StoresPage.tsx</c> tested <c>s.displayName</c> or <c>s.id</c>). Nobody types a GUID into
    /// a search box, but dropping it would be a silent behaviour change rather than a decision.
    /// </summary>
    [Fact]
    public async Task Text_matches_the_store_identifier()
    {
        var (all, _) = await ListAsync(new WatchedStoreFilter(null, null));
        var target = all.First(row => row.DisplayName == "Poki Shop");

        var (rows, _) = await ListAsync(new WatchedStoreFilter(target.Id.ToString(), null));

        rows.Select(row => row.DisplayName).Should().Equal("Poki Shop");
    }

    [Fact]
    public async Task Text_matches_a_fragment_of_the_store_identifier()
    {
        var (all, _) = await ListAsync(new WatchedStoreFilter(null, null));
        var target = all.First(row => row.DisplayName == "Poki Shop");

        var (rows, _) = await ListAsync(new WatchedStoreFilter(target.Id.ToString()[..8], null));

        rows.Should().Contain(row => row.Id == target.Id);
    }

    [Fact]
    public async Task Text_matching_is_case_insensitive()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter("SHROUD", null));

        rows.Should().ContainSingle();
    }

    [Fact]
    public async Task A_text_query_matching_neither_name_nor_identifier_returns_nothing()
    {
        var (rows, total) = await ListAsync(new WatchedStoreFilter("nothing-like-this", null));

        rows.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task Active_state_narrows_to_active_stores()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter(null, IsActive: true));

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => row.IsActive);
    }

    [Fact]
    public async Task Active_state_narrows_to_paused_stores()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter(null, IsActive: false));

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => !row.IsActive);
    }

    [Fact]
    public async Task No_filter_returns_every_store_the_caller_owns()
    {
        var (rows, total) = await ListAsync(new WatchedStoreFilter(null, null));

        rows.Should().HaveCount(4);
        total.Should().Be(4);
    }

    [Fact]
    public async Task Another_owners_stores_are_never_returned()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter(null, null));

        rows.Should().NotContain(row => row.DisplayName == "Someone Elses Store");
    }

    [Fact]
    public async Task A_row_reports_its_item_count_without_carrying_the_items()
    {
        var (rows, _) = await ListAsync(new WatchedStoreFilter("shroud", null));

        rows.Should().ContainSingle().Which.ItemCount.Should().Be(3);
    }

    #region Totals

    [Fact]
    public async Task Totals_are_unaffected_by_the_active_state_filter()
    {
        var unfiltered = await TotalsAsync();

        var (narrowed, _) = await ListAsync(new WatchedStoreFilter(null, IsActive: true));
        narrowed.Should().OnlyContain(row => row.IsActive);

        var afterFiltering = await TotalsAsync();
        afterFiltering.Should().Be(unfiltered);
        afterFiltering.PausedCount.Should().Be(1, "the paused store is still owned");
    }

    [Fact]
    public async Task Totals_are_unaffected_by_the_text_filter()
    {
        var unfiltered = await TotalsAsync();

        await ListAsync(new WatchedStoreFilter("shroud", null));

        (await TotalsAsync()).Should().Be(unfiltered);
    }

    [Fact]
    public async Task Totals_sum_to_the_owned_store_count()
    {
        var totals = await TotalsAsync();

        (totals.ActiveCount + totals.PausedCount).Should().Be(4);
    }

    [Fact]
    public async Task Totals_count_only_the_callers_stores()
    {
        var totals = await TotalsAsync();

        // The other owner has one active store; counting it would make this 4.
        totals.ActiveCount.Should().Be(3);
    }

    [Fact]
    public async Task Totals_are_zero_for_an_owner_with_no_stores()
    {
        await using var scope = CreateScope();

        var totals = await new WatchedStoreRepository(scope.Db).GetTotalsByUserAsync(
            Guid.NewGuid()
        );

        totals.Should().Be(new WatchedStoreTotals(0, 0));
    }

    #endregion

    private async Task<(IReadOnlyList<WatchedStoreListRow> Rows, int Total)> ListAsync(
        WatchedStoreFilter filter
    )
    {
        await using var scope = CreateScope();
        return await new WatchedStoreRepository(scope.Db).ListByUserAsync(
            _userId,
            filter,
            offset: 0,
            limit: 100
        );
    }

    private async Task<WatchedStoreTotals> TotalsAsync()
    {
        await using var scope = CreateScope();
        return await new WatchedStoreRepository(scope.Db).GetTotalsByUserAsync(_userId);
    }

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        var shroud = Store("ch-shroud", "Shroud Store", _userId, isActive: true);
        for (var i = 0; i < 3; i++)
        {
            shroud.AddItem(Item(shroud.Id, $"shroud-{i}", "ch-shroud"));
        }

        scope.Db.WatchedStores.Add(shroud);
        scope.Db.WatchedStores.Add(Store("ch-pokimane", "Poki Shop", _userId, isActive: true));
        scope.Db.WatchedStores.Add(Store("ch-ludwig", "Ludwig Store", _userId, isActive: true));
        scope.Db.WatchedStores.Add(Store("ch-paused", "Paused Store", _userId, isActive: false));
        scope.Db.WatchedStores.Add(
            Store("ch-other", "Someone Elses Store", _otherUserId, isActive: true)
        );

        await scope.Db.SaveChangesAsync();
    }

    private static WatchedStore Store(string channelId, string name, Guid userId, bool isActive)
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId(channelId),
            name,
            userId,
            Now
        );
        if (!isActive)
        {
            store.Deactivate();
        }

        return store;
    }

    private static StoreItem Item(Guid storeId, string seId, string channelId) =>
        new(
            Guid.NewGuid(),
            storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId(seId),
                new StoreExternalId(channelId),
                "Item",
                null,
                "perk",
                100,
                "General",
                true,
                false,
                true,
                false,
                false,
                [],
                [],
                new ItemQuantity(10, 5),
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
