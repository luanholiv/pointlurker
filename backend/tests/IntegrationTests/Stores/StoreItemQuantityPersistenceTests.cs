using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Stores;

/// <summary>
/// Stock has to be a queryable column, not an opaque <c>jsonb</c> blob: availability is the default
/// filter on the item grid and it is expressed in terms of <c>Quantity.Current</c>. These tests pin
/// both halves — the values round-trip through real integer columns, and a predicate over them is
/// evaluated by the database rather than after the rows have been transferred.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class StoreItemQuantityPersistenceTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Quantity_round_trips_through_the_new_columns()
    {
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = CreateStore(storeId, "ch-qty-roundtrip");
            store.AddItem(
                new StoreItem(itemId, storeId, CreateApiData("se-1", new ItemQuantity(10, 3)))
            );
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var item = await db.StoreItems.FirstAsync(i => i.Id == itemId);

            item.Quantity.Total.Should().Be(10);
            item.Quantity.Current.Should().Be(3);
        }
    }

    [Fact]
    public async Task Unlimited_stock_survives_persistence()
    {
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = CreateStore(storeId, "ch-qty-unlimited");
            store.AddItem(
                new StoreItem(
                    itemId,
                    storeId,
                    CreateApiData("se-unlimited", new ItemQuantity(-1, -1))
                )
            );
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var item = await db.StoreItems.FirstAsync(i => i.Id == itemId);

            item.Quantity.Total.Should().Be(-1);
            item.Quantity.Current.Should().Be(-1);
        }
    }

    [Fact]
    public async Task Stock_is_stored_in_integer_columns()
    {
        await using var db = CreateDbContext();

        var columns = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT column_name || ':' || data_type AS "Value" FROM information_schema.columns
                WHERE table_name = 'store_items' AND column_name IN ('QuantityTotal', 'QuantityCurrent')
                """
            )
            .ToListAsync();

        columns.Should().BeEquivalentTo("QuantityTotal:integer", "QuantityCurrent:integer");
    }

    [Fact]
    public async Task The_jsonb_quantity_column_is_gone()
    {
        await using var db = CreateDbContext();

        var columns = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'store_items'
                """
            )
            .ToListAsync();

        columns.Should().NotContain("Quantity");
    }

    [Fact]
    public async Task A_stock_predicate_is_translated_to_SQL()
    {
        await using var db = CreateDbContext();

        var sql = db.StoreItems.Where(item => item.Quantity.Current > 0).ToQueryString();

        sql.Should().Contain("QuantityCurrent");
        sql.Should().Contain("> 0");
    }

    [Fact]
    public async Task A_stock_predicate_is_evaluated_by_the_database()
    {
        var storeId = Guid.NewGuid();
        var inStockId = Guid.NewGuid();
        var depletedId = Guid.NewGuid();
        var unlimitedId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = CreateStore(storeId, "ch-qty-predicate");
            store.AddItem(
                new StoreItem(
                    inStockId,
                    storeId,
                    CreateApiData("se-in-stock", new ItemQuantity(10, 4))
                )
            );
            store.AddItem(
                new StoreItem(
                    depletedId,
                    storeId,
                    CreateApiData("se-depleted", new ItemQuantity(10, 0))
                )
            );
            store.AddItem(
                new StoreItem(
                    unlimitedId,
                    storeId,
                    CreateApiData("se-unlimited", new ItemQuantity(-1, -1))
                )
            );
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var matches = await db
                .StoreItems.Where(item =>
                    item.WatchedStoreId == storeId && item.Quantity.Current > 0
                )
                .Select(item => item.Id)
                .ToListAsync();

            matches.Should().BeEquivalentTo([inStockId]);
        }
    }

    [Fact]
    public async Task The_availability_expression_is_translated_to_SQL()
    {
        await using var db = CreateDbContext();

        var sql = db.StoreItems.Where(StoreItem.IsAvailableExpression).ToQueryString();

        sql.Should().Contain("QuantityCurrent");
        sql.Should().Contain("Enabled");
    }

    [Fact]
    public async Task The_availability_expression_selects_the_same_items_in_SQL_as_in_memory()
    {
        var storeId = Guid.NewGuid();
        var enabledInStock = Guid.NewGuid();
        var enabledUnlimited = Guid.NewGuid();
        var enabledDepleted = Guid.NewGuid();
        var disabledInStock = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = CreateStore(storeId, "ch-qty-availability");
            store.AddItem(
                new StoreItem(
                    enabledInStock,
                    storeId,
                    CreateApiData("se-a", new ItemQuantity(10, 4), enabled: true)
                )
            );
            store.AddItem(
                new StoreItem(
                    enabledUnlimited,
                    storeId,
                    CreateApiData("se-b", new ItemQuantity(-1, -1), enabled: true)
                )
            );
            store.AddItem(
                new StoreItem(
                    enabledDepleted,
                    storeId,
                    CreateApiData("se-c", new ItemQuantity(10, 0), enabled: true)
                )
            );
            store.AddItem(
                new StoreItem(
                    disabledInStock,
                    storeId,
                    CreateApiData("se-d", new ItemQuantity(10, 4), enabled: false)
                )
            );
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var fromSql = await db
                .StoreItems.Where(item => item.WatchedStoreId == storeId)
                .Where(StoreItem.IsAvailableExpression)
                .Select(item => item.Id)
                .ToListAsync();

            var inMemory = (
                await db.StoreItems.Where(item => item.WatchedStoreId == storeId).ToListAsync()
            )
                .Where(item => item.Availability.IsAvailable)
                .Select(item => item.Id)
                .ToList();

            fromSql.Should().BeEquivalentTo([enabledInStock, enabledUnlimited]);
            fromSql.Should().BeEquivalentTo(inMemory);
        }
    }

    private static WatchedStore CreateStore(Guid storeId, string channelId) =>
        new(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId(channelId),
            "Test",
            Guid.NewGuid(),
            Now
        );

    private static StoreItemApiData CreateApiData(
        string seId,
        ItemQuantity quantity,
        bool enabled = true
    ) =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId(seId),
            new StoreExternalId("ch-qty"),
            "Item",
            null,
            "perk",
            100,
            "default",
            enabled,
            false,
            true,
            false,
            false,
            [],
            [],
            quantity,
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
