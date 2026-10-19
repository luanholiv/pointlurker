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
/// Ordering parity with the client comparator this suite replaces.
///
/// The fixture and every expected order below were <b>captured</b>, not designed: each was produced
/// by running <c>useFilteredItems.ts</c>'s comparator over this exact item set and recording the
/// result. They are what that implementation <i>did</i>, not what it ought to have done — that is
/// the point of them.
///
/// So an expectation here cannot be re-derived by rerunning anything: the comparator is gone.
/// Changing one is changing the definition of parity, and turns a measured property back into a
/// claim. The one deliberate exception is the date sort, which has no fixture at all — see D16.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class StoreItemOrderingParityTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private static readonly Guid StoreId = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    /// <summary>
    /// Ids ascend with fixture order so the client's stable sort and this query's <c>Id</c>
    /// tiebreaker describe the same order for tied keys — without that the cost ties could not be
    /// compared between the two implementations at all.
    /// </summary>
    private static readonly (string Id, string Name, long Cost, int Current)[] Fixture =
    [
        ("00000000-0000-0000-0000-000000000001", "Zebra", 100, 5),
        ("00000000-0000-0000-0000-000000000002", "arvore", 9, -1),
        ("00000000-0000-0000-0000-000000000003", "Árvore", 1000, 0),
        ("00000000-0000-0000-0000-000000000004", "banana", 100, 12),
        ("00000000-0000-0000-0000-000000000005", "Émoji", 50, 3),
    ];

    public static TheoryData<StoreItemSortField, SortDirection, string[]> GoldenOrders =>
        new()
        {
            {
                StoreItemSortField.Name,
                SortDirection.Asc,
                ["arvore", "Árvore", "banana", "Émoji", "Zebra"]
            },
            {
                StoreItemSortField.Name,
                SortDirection.Desc,
                ["Zebra", "Émoji", "banana", "Árvore", "arvore"]
            },
            {
                StoreItemSortField.Cost,
                SortDirection.Asc,
                ["arvore", "Émoji", "Zebra", "banana", "Árvore"]
            },
            // Zebra and banana both cost 100. A descending sort negates the comparator, but negating
            // zero is still zero, so the client does not reverse ties — which is why the tiebreaker
            // here stays ascending even when the primary key is descending.
            {
                StoreItemSortField.Cost,
                SortDirection.Desc,
                ["Árvore", "Zebra", "banana", "Émoji", "arvore"]
            },
            {
                StoreItemSortField.Stock,
                SortDirection.Asc,
                ["Árvore", "Émoji", "Zebra", "banana", "arvore"]
            },
            {
                StoreItemSortField.Stock,
                SortDirection.Desc,
                ["arvore", "banana", "Zebra", "Émoji", "Árvore"]
            },
        };

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [MemberData(nameof(GoldenOrders))]
    public async Task Repository_reproduces_the_captured_client_ordering(
        StoreItemSortField sort,
        SortDirection direction,
        string[] expected
    )
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, total) = await repository.ListAsync(
            StoreId,
            FilterBy(sort, direction),
            offset: 0,
            limit: 50
        );

        rows.Select(row => row.Name).Should().Equal(expected);
        total.Should().Be(Fixture.Length);
    }

    [Fact]
    public async Task Unlimited_stock_sorts_last_ascending()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Stock, SortDirection.Asc),
            offset: 0,
            limit: 50
        );

        rows[^1].QuantityCurrent.Should().Be(-1);
        rows.SkipLast(1).Should().OnlyContain(row => row.QuantityCurrent >= 0);
    }

    [Fact]
    public async Task Unlimited_stock_sorts_first_descending()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Stock, SortDirection.Desc),
            offset: 0,
            limit: 50
        );

        rows[0].QuantityCurrent.Should().Be(-1);
        rows.Skip(1).Should().OnlyContain(row => row.QuantityCurrent >= 0);
    }

    [Fact]
    public async Task Name_ordering_is_case_insensitive_and_accent_aware()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Name, SortDirection.Asc),
            offset: 0,
            limit: 50
        );

        var order = rows.Select(row => row.Name).ToList();

        // A byte-wise ordinal comparison puts every accented and lowercase name after "Zebra".
        order.IndexOf("arvore").Should().BeLessThan(order.IndexOf("Árvore"));
        order.IndexOf("Árvore").Should().BeLessThan(order.IndexOf("banana"));
        order.IndexOf("Émoji").Should().BeLessThan(order.IndexOf("Zebra"));
    }

    [Fact]
    public async Task Cost_ordering_is_numeric_not_lexicographic()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Cost, SortDirection.Asc),
            offset: 0,
            limit: 50
        );

        rows.Select(row => row.Cost).Should().Equal(9, 50, 100, 100, 1000);
    }

    /// <summary>
    /// The date sort has no golden fixture on purpose: the response it replaced never carried
    /// <c>ProviderCreatedAt</c>, so the client compared "" to "" and reordered nothing. This asserts the
    /// repaired behaviour instead (see <c>design.md</c> D16).
    /// </summary>
    [Fact]
    public async Task Date_ordering_puts_the_oldest_first_ascending()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Date, SortDirection.Asc),
            offset: 0,
            limit: 50
        );

        rows.Select(row => row.ProviderCreatedAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Date_ordering_puts_the_newest_first_descending()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Date, SortDirection.Desc),
            offset: 0,
            limit: 50
        );

        rows.Select(row => row.ProviderCreatedAt).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sorting_defaults_to_name_ascending()
    {
        await using var scope = CreateScope();
        var repository = new StoreItemRepository(scope.Db);

        var (rows, _) = await repository.ListAsync(
            StoreId,
            FilterBy(StoreItemSortField.Name, SortDirection.Asc),
            offset: 0,
            limit: 50
        );

        rows.Select(row => row.Name).Should().Equal("arvore", "Árvore", "banana", "Émoji", "Zebra");
    }

    private static StoreItemFilter FilterBy(StoreItemSortField sort, SortDirection direction) =>
        new(
            SearchTerm: null,
            AvailableOnly: false,
            SubscriberOnly: null,
            Featured: null,
            Categories: null,
            MinCost: null,
            MaxCost: null,
            SortField: sort,
            SortDirection: direction
        );

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        var store = new WatchedStore(
            StoreId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-parity"),
            "Parity",
            Guid.NewGuid(),
            Now
        );

        var index = 0;
        foreach (var (id, name, cost, current) in Fixture)
        {
            store.AddItem(
                new StoreItem(
                    Guid.Parse(id),
                    StoreId,
                    new StoreItemApiData(
                        StoreProvider.StreamElements,
                        new ProviderItemId($"se-{index}"),
                        new StoreExternalId("ch-parity"),
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
                        [],
                        [],
                        new ItemQuantity(1000, current),
                        new ItemCooldown(0, 0, 0),
                        // Creation timestamps ascend with fixture order, so the date sort has
                        // something to order by that is not the insertion order.
                        Now.AddDays(index),
                        null,
                        Now
                    )
                )
            );
            index++;
        }

        scope.Db.WatchedStores.Add(store);
        await scope.Db.SaveChangesAsync();
    }

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
