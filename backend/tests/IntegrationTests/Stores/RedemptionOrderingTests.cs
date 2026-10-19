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
/// Ordering the redemption history in the database. The page this replaces sorted
/// <c>data.items</c> — the twenty rows it had already been given — and presented the result as a
/// sorted table, so the property that matters is that the ordering spans the whole result set and
/// the first slice really does hold the globally first rows.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class RedemptionOrderingTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private const int Total = 25;
    private const int PageSize = 10;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ascending_order_puts_the_globally_smallest_values_on_the_first_page()
    {
        var everything = await ListAsync(
            Filter(RedemptionSortField.StoreItemName, SortDirection.Asc),
            0,
            Total
        );
        var firstPage = await ListAsync(
            Filter(RedemptionSortField.StoreItemName, SortDirection.Asc),
            0,
            PageSize
        );

        firstPage
            .Select(row => row.StoreItemName)
            .Should()
            .Equal(everything.Take(PageSize).Select(row => row.StoreItemName));

        // Nothing outside the first page sorts before anything on it.
        var lastOnPage = firstPage[^1].StoreItemName;
        everything
            .Skip(PageSize)
            .Should()
            .OnlyContain(row =>
                string.Compare(row.StoreItemName, lastOnPage, StringComparison.OrdinalIgnoreCase)
                >= 0
            );
    }

    [Fact]
    public async Task Ascending_and_descending_are_reverses_of_one_another()
    {
        var ascending = await ListAsync(
            Filter(RedemptionSortField.AttemptedAt, SortDirection.Asc),
            0,
            Total
        );
        var descending = await ListAsync(
            Filter(RedemptionSortField.AttemptedAt, SortDirection.Desc),
            0,
            Total
        );

        ascending.Select(row => row.Id).Should().Equal(descending.Select(row => row.Id).Reverse());
    }

    /// <summary>
    /// The two nullable columns order their present values ascending and put the absent ones at one
    /// end — PostgreSQL's NULLS LAST. The assertion skips the nulls rather than treating them as a
    /// smallest value, which is what a naive ordering check would do; where they land is asserted
    /// separately.
    /// </summary>
    [Theory]
    [InlineData(RedemptionSortField.Status)]
    [InlineData(RedemptionSortField.StoreItemName)]
    [InlineData(RedemptionSortField.StoreName)]
    [InlineData(RedemptionSortField.AttemptedAt)]
    [InlineData(RedemptionSortField.SubmittedAt)]
    [InlineData(RedemptionSortField.SettledAt)]
    public async Task Every_presented_column_orders_the_whole_result_set(RedemptionSortField field)
    {
        var rows = await ListAsync(Filter(field, SortDirection.Asc), 0, Total);

        rows.Should().HaveCount(Total);
        Values(rows, field).Where(value => value is not null).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Ordering_defaults_to_attempted_time_most_recent_first()
    {
        var rows = await ListAsync(new RedemptionFilter(null, null, null, null), 0, Total);

        rows.Select(row => row.AttemptedAt).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// Half the fixture has never been submitted, so a sort on that column has to place the nulls
    /// somewhere. Which end matters less than that it is the same end every time.
    /// </summary>
    [Fact]
    public async Task Absent_timestamps_group_at_one_end_and_stay_there()
    {
        var first = await ListAsync(
            Filter(RedemptionSortField.SubmittedAt, SortDirection.Asc),
            0,
            Total
        );
        var second = await ListAsync(
            Filter(RedemptionSortField.SubmittedAt, SortDirection.Asc),
            0,
            Total
        );

        var nullPositions = first
            .Select((row, index) => (row.SubmittedAt, index))
            .Where(pair => pair.SubmittedAt is null)
            .Select(pair => pair.index)
            .ToList();

        nullPositions.Should().NotBeEmpty("the fixture must contain unsubmitted redemptions");
        // Contiguous: a run of nulls, not nulls scattered through the ordering.
        nullPositions.Should().Equal(Enumerable.Range(nullPositions[0], nullPositions.Count));

        first.Select(row => row.Id).Should().Equal(second.Select(row => row.Id));
    }

    [Fact]
    public async Task Ordering_combines_with_the_status_filter()
    {
        var rows = await ListAsync(
            Filter(RedemptionSortField.StoreItemName, SortDirection.Asc) with
            {
                Status = RedemptionStatus.Pending,
            },
            0,
            Total
        );

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => row.Status == RedemptionStatus.Pending);
        rows.Select(row => row.StoreItemName.ToLowerInvariant()).Should().BeInAscendingOrder();
    }

    /// <summary>
    /// Every row shares one store name, so only the Id tiebreaker separates them. Without it,
    /// walking the slices repeats some rows and drops others.
    /// </summary>
    [Fact]
    public async Task Paging_over_tied_sort_keys_loses_and_repeats_nothing()
    {
        var filter = Filter(RedemptionSortField.StoreName, SortDirection.Asc);

        var seen = new List<Guid>();
        for (var offset = 0; offset < Total; offset += 4)
        {
            var page = await ListAsync(filter, offset, 4);
            seen.AddRange(page.Select(row => row.Id));
        }

        seen.Should().HaveCount(Total);
        seen.Should().OnlyHaveUniqueItems();
    }

    private static RedemptionFilter Filter(RedemptionSortField field, SortDirection direction) =>
        new(null, null, null, null, field, direction);

    private static IEnumerable<IComparable?> Values(
        IReadOnlyList<RedemptionListRow> rows,
        RedemptionSortField field
    ) =>
        field switch
        {
            // Status is stored as its name, not its ordinal, so it orders alphabetically.
            RedemptionSortField.Status => rows.Select(row => (IComparable?)row.Status.ToString()),
            RedemptionSortField.StoreItemName => rows.Select(row =>
                (IComparable?)row.StoreItemName.ToLowerInvariant()
            ),
            RedemptionSortField.StoreName => rows.Select(row =>
                (IComparable?)row.StoreName.ToLowerInvariant()
            ),
            RedemptionSortField.SubmittedAt => rows.Select(row => (IComparable?)row.SubmittedAt),
            RedemptionSortField.SettledAt => rows.Select(row => (IComparable?)row.SettledAt),
            _ => rows.Select(row => (IComparable?)row.AttemptedAt),
        };

    private async Task<IReadOnlyList<RedemptionListRow>> ListAsync(
        RedemptionFilter filter,
        int offset,
        int limit
    )
    {
        await using var scope = CreateScope();
        var (rows, _) = await new RedemptionRepository(scope.Db).ListAsync(filter, offset, limit);
        return rows;
    }

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-order"),
            "Order",
            userId,
            Now
        );
        var item = new StoreItem(
            Guid.NewGuid(),
            storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-order"),
                new StoreExternalId("ch-order"),
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
                new ItemQuantity(-1, -1),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );
        store.AddItem(item);
        scope.Db.WatchedStores.Add(store);

        var entry = new WishlistEntry(Guid.NewGuid(), userId, item.Id, Now);
        scope.Db.WishlistEntries.Add(entry);
        await scope.Db.SaveChangesAsync();

        // Names ascend with index so an ordering assertion has something to bite on; the store name
        // is shared by every row, which is what exercises the tiebreaker.
        for (var i = 0; i < Total; i++)
        {
            var redemption = entry.CreateRedemption(
                item,
                sentMessage: null,
                storeItemName: $"Item {i:D3}",
                storeName: "Order",
                now: Now.AddMinutes(i)
            );
            scope.Db.Redemptions.Add(redemption);
        }

        await scope.Db.SaveChangesAsync();

        // Half get a submitted timestamp and a terminal status, leaving the rest with nulls: a
        // nullable-column sort needs both to have anything to say. Written in SQL because the
        // domain reaches those states through a submission flow this test has no interest in.
        await scope.Db.Database.ExecuteSqlRawAsync(
            """
            UPDATE redemptions
            SET "SubmittedAt" = CASE WHEN numbered.row_number % 2 = 0
                                     THEN redemptions."AttemptedAt" + interval '1 minute' END,
                "SettledAt" = CASE WHEN numbered.row_number % 2 = 0
                                       THEN redemptions."AttemptedAt" + interval '2 minute' END,
                "Status" = CASE WHEN numbered.row_number % 2 = 0 THEN 'Completed' ELSE 'Pending' END
            FROM (
                SELECT "Id", ROW_NUMBER() OVER (ORDER BY "AttemptedAt") AS row_number
                FROM redemptions
            ) AS numbered
            WHERE redemptions."Id" = numbered."Id"
            """
        );
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
