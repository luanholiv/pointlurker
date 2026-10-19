using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the MakeItemQuantityQueryable migration: stock moves off the opaque <c>jsonb</c> blob
/// onto two integer columns, backfilled from the existing payload. The stored JSON is camelCase
/// (the converter used <c>JsonSerializerDefaults.Web</c>), so a backfill reading <c>-&gt;&gt;'Total'</c>
/// would silently produce NULLs rather than fail — which is exactly what these assertions catch.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class ItemQuantityColumnsMigrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string PreviousMigration = "20260622200127_HardenRedemptionInputData";

    private string _connectionString = null!;

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container: the fixture's main database is already at
        // the latest migration, but this test has to start one migration back to exercise Up()'s
        // data backfill against rows that still hold the jsonb payload.
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"qty_mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_backfills_every_row_from_the_camelCase_payload()
    {
        var storeId = Guid.NewGuid();
        var finiteId = Guid.NewGuid();
        var unlimitedId = Guid.NewGuid();
        var depletedId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO watched_stores ("Id", "ChannelId", "DisplayName", "UserId", "IsActive", "CreatedAt")
                VALUES ({0}, 'ch-qty-mig', 'Quantity Migration', {1}, true, now());
                """,
                storeId,
                Guid.NewGuid()
            );

            await InsertPreMigrationItem(
                db,
                finiteId,
                storeId,
                "se-finite",
                """{"total":500,"current":309}"""
            );
            await InsertPreMigrationItem(
                db,
                unlimitedId,
                storeId,
                "se-unlimited",
                """{"total":-1,"current":-1}"""
            );
            await InsertPreMigrationItem(
                db,
                depletedId,
                storeId,
                "se-depleted",
                """{"total":10,"current":0}"""
            );

            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var nulls = await db
                .Database.SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value" FROM store_items
                    WHERE "QuantityTotal" IS NULL OR "QuantityCurrent" IS NULL
                    """
                )
                .SingleAsync();
            nulls.Should().Be(0, "the backfill must reach every pre-migration row");

            var items = await db.StoreItems.ToListAsync();

            items.Single(i => i.Id == finiteId).Quantity.Total.Should().Be(500);
            items.Single(i => i.Id == finiteId).Quantity.Current.Should().Be(309);
            items.Single(i => i.Id == unlimitedId).Quantity.Total.Should().Be(-1);
            items.Single(i => i.Id == unlimitedId).Quantity.Current.Should().Be(-1);
            items.Single(i => i.Id == depletedId).Quantity.Total.Should().Be(10);
            items.Single(i => i.Id == depletedId).Quantity.Current.Should().Be(0);
        }
    }

    [Fact]
    public async Task Migration_drops_the_jsonb_column_and_adds_the_search_indexes()
    {
        await using (var db = CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var columns = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT column_name AS "Value" FROM information_schema.columns
                    WHERE table_name = 'store_items'
                    """
                )
                .ToListAsync();

            columns.Should().Contain(["QuantityTotal", "QuantityCurrent"]);
            columns.Should().NotContain("Quantity");

            var indexes = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexname AS "Value" FROM pg_indexes WHERE tablename = 'store_items'
                    """
                )
                .ToListAsync();

            indexes
                .Should()
                .Contain([
                    "IX_store_items_WatchedStoreId_Enabled_QuantityCurrent",
                    "IX_store_items_WatchedStoreId_CategoryName",
                ]);
        }
    }

    private static Task InsertPreMigrationItem(
        AppDbContext db,
        Guid itemId,
        Guid storeId,
        string seId,
        string quantityJson
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO store_items
                ("Id", "WatchedStoreId", "StreamElementsId", "ChannelId", "Name", "Type", "Cost",
                 "CategoryName", "Enabled", "Featured", "Public", "SubscriberOnly", "AllowMessages",
                 "UserInputDefinitions", "Sources", "Quantity", "Cooldown", "SeCreatedAt", "LastFetchedAt")
            VALUES ({0}, {1}, {2}, 'ch-qty-mig', 'Item', 'perk', 100, 'default', true, false, true,
                    false, false, '[]'::jsonb, '[]'::jsonb, {3}::jsonb, {4}::jsonb, now(), now());
            """,
            itemId,
            storeId,
            seId,
            quantityJson,
            """{"userMs":0,"globalMs":0,"categoryMs":0}"""
        );

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
