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
/// Verifies the AddStoreProvider migration, the expand step of the provider-neutral rename. It must
/// stay additive: the release before it has to keep running against the migrated schema, since
/// rolling back an image does not roll back the database.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class AddStoreProviderMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260921134605_AddWishlistEntryDesiredRedemptions";
    private const string ExpandMigration = "20260928170212_AddStoreProvider";

    private static readonly string[] Tables = ["watched_stores", "store_items", "redemptions"];

    private static readonly DateTimeOffset CreatedAt = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = new(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public AddStoreProviderMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container: the fixture's main database is already at
        // the latest migration, but this test must seed rows at the previous one.
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_removes_no_column_the_previous_release_reads_or_writes()
    {
        IReadOnlyList<string> before;
        IReadOnlyList<string> after;

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            before = await ColumnsAsync(db);

            await migrator.MigrateAsync(ExpandMigration);
            after = await ColumnsAsync(db);
        }

        after.Should().Contain(before);
        after
            .Except(before)
            .Should()
            .BeEquivalentTo(
                "watched_stores.Provider",
                "store_items.Provider",
                "redemptions.Provider"
            );
    }

    [Fact]
    public async Task Rows_written_the_previous_releases_way_read_back_as_StreamElements()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var redemptionId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            // Rows seeded before the migration stand for existing data; the redemption inserted
            // after it stands for the previous release still running, which knows nothing of
            // Provider.
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO watched_stores ("Id", "ChannelId", "DisplayName", "UserId", "IsActive", "CreatedAt")
                VALUES ({0}, 'ch-provider-mig', 'Store', {1}, true, now());
                """,
                storeId,
                userId
            );

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO store_items
                    ("Id", "WatchedStoreId", "StreamElementsId", "ChannelId", "Name", "Type", "Cost",
                     "CategoryName", "Enabled", "Featured", "Public", "SubscriberOnly", "AllowMessages",
                     "UserInputDefinitions", "Sources", "QuantityTotal", "QuantityCurrent", "Cooldown",
                     "SeCreatedAt", "SeUpdatedAt", "LastChangedAt")
                VALUES ({0}, {1}, 'se-item-mig', 'ch-provider-mig', 'Item', 'perk', 100, 'default',
                        true, false, true, false, false, '[]'::jsonb, '[]'::jsonb, -1, -1,
                        {2}::jsonb, {3}, {4}, now());
                """,
                itemId,
                storeId,
                """{"userMs":0,"globalMs":0,"categoryMs":0}""",
                CreatedAt,
                UpdatedAt
            );

            await migrator.MigrateAsync(ExpandMigration);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO redemptions
                    ("Id", "WishlistEntryId", "StoreItemId", "StoreItemName", "StoreName", "ChannelId",
                     "StreamElementsItemId", "StreamElementsRedemptionId", "Status", "Attempts", "AttemptedAt")
                VALUES ({0}, gen_random_uuid(), {1}, 'Item', 'Store', 'ch-provider-mig',
                        'se-item-mig', 'se-redemption-mig', 'Submitted', 1, now());
                """,
                redemptionId,
                itemId
            );

            // The contract step renames the columns under those rows; the model reads the new names.
            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var store = await db.WatchedStores.SingleAsync(s => s.Id == storeId);
            store.Provider.Should().Be(StoreProvider.StreamElements);
            store.ExternalId.Should().Be(new StoreExternalId("ch-provider-mig"));

            var item = await db.StoreItems.SingleAsync(i => i.Id == itemId);
            item.Provider.Should().Be(StoreProvider.StreamElements);
            item.ProviderItemId.Should().Be(new ProviderItemId("se-item-mig"));
            item.StoreExternalId.Should().Be(new StoreExternalId("ch-provider-mig"));
            item.ProviderCreatedAt.Should().Be(CreatedAt);
            item.ProviderUpdatedAt.Should().Be(UpdatedAt);

            var redemption = await db.Redemptions.SingleAsync(r => r.Id == redemptionId);
            redemption.Provider.Should().Be(StoreProvider.StreamElements);
            redemption.StoreExternalId.Should().Be(new StoreExternalId("ch-provider-mig"));
            redemption.ProviderItemId.Should().Be(new ProviderItemId("se-item-mig"));
            redemption.ProviderRedemptionId.Should().Be("se-redemption-mig");
        }
    }

    [Fact]
    public async Task Store_uniqueness_is_per_provider()
    {
        var userId = Guid.NewGuid();

        await using var db = CreateDbContext();
        await db.GetService<IMigrator>().MigrateAsync(ExpandMigration);

        await InsertStoreAsync(db, StoreProvider.StreamElements, userId);

        // The same external id under another provider is a different store...
        var inserted = await InsertStoreAsync(db, StoreProvider.GauPoints, userId);
        inserted.Should().Be(1);

        // ...while under the same provider it is still a duplicate.
        var duplicate = () => InsertStoreAsync(db, StoreProvider.StreamElements, userId);
        await duplicate
            .Should()
            .ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    private static Task<int> InsertStoreAsync(
        AppDbContext db,
        StoreProvider provider,
        Guid userId
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO watched_stores ("Id", "Provider", "ChannelId", "DisplayName", "UserId", "IsActive", "CreatedAt")
            VALUES (gen_random_uuid(), {0}, 'ch-unique', 'Store', {1}, true, now());
            """,
            (int)provider,
            userId
        );

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
