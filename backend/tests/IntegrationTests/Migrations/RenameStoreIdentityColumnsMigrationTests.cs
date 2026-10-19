using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the RenameStoreIdentityColumns migration, the contract step after AddStoreProvider: it
/// renames columns and one index, carries every value across, and changes nothing else.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class RenameStoreIdentityColumnsMigrationTests : IAsyncLifetime
{
    private const string ExpandMigration = "20260928170212_AddStoreProvider";
    private const string ContractMigration = "20260928185736_RenameStoreIdentityColumns";

    private static readonly string[] Tables = ["watched_stores", "store_items", "redemptions"];

    private static readonly (string Old, string New)[] Renames =
    [
        ("watched_stores.ChannelId", "watched_stores.ExternalId"),
        ("store_items.StreamElementsId", "store_items.ProviderItemId"),
        ("store_items.ChannelId", "store_items.StoreExternalId"),
        ("store_items.SeCreatedAt", "store_items.ProviderCreatedAt"),
        ("store_items.SeUpdatedAt", "store_items.ProviderUpdatedAt"),
        ("redemptions.ChannelId", "redemptions.StoreExternalId"),
        ("redemptions.StreamElementsItemId", "redemptions.ProviderItemId"),
        ("redemptions.StreamElementsRedemptionId", "redemptions.ProviderRedemptionId"),
    ];

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public RenameStoreIdentityColumnsMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container: the fixture's main database is already at
        // the latest migration, but this test must seed rows at the expand step.
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_renames_exactly_the_store_identity_columns()
    {
        IReadOnlyList<string> before;
        IReadOnlyList<string> after;
        IReadOnlyList<string> indexes;

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);
            before = await ColumnsAsync(db);

            await migrator.MigrateAsync(ContractMigration);
            after = await ColumnsAsync(db);
            indexes = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexname AS "Value" FROM pg_indexes
                    WHERE tablename = 'watched_stores'
                    """
                )
                .ToListAsync();
        }

        var expected = before
            .Select(column => Renames.SingleOrDefault(r => r.Old == column).New ?? column)
            .ToList();
        after.Should().BeEquivalentTo(expected);

        indexes
            .Should()
            .Contain("IX_watched_stores_UserId_Provider_ExternalId")
            .And.NotContain("IX_watched_stores_UserId_Provider_ChannelId");
    }

    [Fact]
    public async Task Migration_carries_every_value_across_including_non_StreamElements_rows()
    {
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var redemptionId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO watched_stores ("Id", "Provider", "ChannelId", "DisplayName", "UserId", "IsActive", "CreatedAt")
                VALUES ({0}, {1}, 'gaupoints', 'Store', gen_random_uuid(), true, now());
                """,
                storeId,
                (int)StoreProvider.GauPoints
            );

            // No provider dates: the column went nullable in the expand step so this row can exist.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO store_items
                    ("Id", "WatchedStoreId", "Provider", "StreamElementsId", "ChannelId", "Name", "Type",
                     "Cost", "CategoryName", "Enabled", "Featured", "Public", "SubscriberOnly",
                     "AllowMessages", "UserInputDefinitions", "Sources", "QuantityTotal",
                     "QuantityCurrent", "Cooldown", "SeCreatedAt", "SeUpdatedAt", "LastChangedAt")
                VALUES ({0}, {1}, {2}, 'gift-card-10', 'gaupoints', 'Item', 'digital', 100, 'default',
                        true, false, true, false, false, '[]'::jsonb, '[]'::jsonb, 3, 3,
                        {3}::jsonb, NULL, NULL, now());
                """,
                itemId,
                storeId,
                (int)StoreProvider.GauPoints,
                """{"userMs":0,"globalMs":0,"categoryMs":0}"""
            );

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO redemptions
                    ("Id", "WishlistEntryId", "StoreItemId", "StoreItemName", "StoreName", "Provider",
                     "ChannelId", "StreamElementsItemId", "StreamElementsRedemptionId", "Status",
                     "Attempts", "AttemptedAt")
                VALUES ({0}, gen_random_uuid(), {1}, 'Item', 'Store', {2}, 'gaupoints',
                        'gift-card-10', NULL, 'Submitted', 1, now());
                """,
                redemptionId,
                itemId,
                (int)StoreProvider.GauPoints
            );

            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var store = await db.WatchedStores.SingleAsync(s => s.Id == storeId);
            store.Provider.Should().Be(StoreProvider.GauPoints);
            store.ExternalId.Should().Be(new StoreExternalId("gaupoints"));

            var item = await db.StoreItems.SingleAsync(i => i.Id == itemId);
            item.Provider.Should().Be(StoreProvider.GauPoints);
            item.ProviderItemId.Should().Be(new ProviderItemId("gift-card-10"));
            item.StoreExternalId.Should().Be(new StoreExternalId("gaupoints"));
            item.ProviderCreatedAt.Should().BeNull();
            item.ProviderUpdatedAt.Should().BeNull();

            var redemption = await db.Redemptions.SingleAsync(r => r.Id == redemptionId);
            redemption.Provider.Should().Be(StoreProvider.GauPoints);
            redemption.StoreExternalId.Should().Be(new StoreExternalId("gaupoints"));
            redemption.ProviderItemId.Should().Be(new ProviderItemId("gift-card-10"));
            redemption.ProviderRedemptionId.Should().BeNull();
        }
    }

    private static async Task<IReadOnlyList<string>> ColumnsAsync(AppDbContext db) =>
        await db
            .Database.SqlQuery<string>(
                $"""
                SELECT table_name || '.' || column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = ANY({Tables})
                """
            )
            .ToListAsync();

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
