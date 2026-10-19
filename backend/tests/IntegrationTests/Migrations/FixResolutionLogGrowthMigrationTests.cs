using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the FixResolutionLogGrowth migration: deduplicates the resolution
/// log backlog to the newest row per (StoreId, RawLabel), drops the unused
/// StoreItemId column, and adds the composite lookup index.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class FixResolutionLogGrowthMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260527191756_DropFailureReasonColumn";

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public FixResolutionLogGrowthMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container: the fixture's main
        // database is already at the latest migration, but this test must
        // start from the previous one to exercise the Up() data cleanup.
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_dedupes_backlog_drops_item_column_and_adds_index()
    {
        var storeId = Guid.NewGuid();
        var otherStoreId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO watched_stores ("Id", "ChannelId", "DisplayName", "UserId", "IsActive", "CreatedAt")
                VALUES ({0}, 'ch-mig-a', 'A', {2}, true, now()),
                       ({1}, 'ch-mig-b', 'B', {2}, true, now());
                """,
                storeId,
                otherStoreId,
                Guid.NewGuid()
            );

            // Pre-migration shape: duplicate per-item rows per label, with the
            // legacy StoreItemId column still present.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO store_input_resolution_log
                    ("Id", "StoreId", "StoreItemId", "RawLabel", "CanonicalLabel", "Confidence", "Source", "ObservedAt")
                VALUES
                    (gen_random_uuid(), {0}, gen_random_uuid(), 'steam id', NULL, 30, 'Unresolved', '2026-06-01T00:00:00Z'),
                    (gen_random_uuid(), {0}, gen_random_uuid(), 'steam id', NULL, 35, 'Unresolved', '2026-06-02T00:00:00Z'),
                    (gen_random_uuid(), {0}, gen_random_uuid(), 'steam id', NULL, 42, 'Unresolved', '2026-06-03T00:00:00Z'),
                    (gen_random_uuid(), {0}, gen_random_uuid(), 'discord tag', 'Discord Tag', 91, 'Fuzzy', '2026-06-02T12:00:00Z'),
                    (gen_random_uuid(), {0}, gen_random_uuid(), 'discord tag', NULL, 10, 'Unresolved', '2026-06-01T12:00:00Z'),
                    (gen_random_uuid(), {1}, gen_random_uuid(), 'steam id', NULL, 77, 'Unresolved', '2026-06-01T06:00:00Z');
                """,
                storeId,
                otherStoreId
            );

            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var survivors = await db
                .StoreInputResolutionLog.AsNoTracking()
                .OrderBy(e => e.RawLabel)
                .ToListAsync();

            // One row per (StoreId, RawLabel), each the newest of its group.
            survivors.Should().HaveCount(3);
            survivors
                .Single(e => e.StoreId == storeId && e.RawLabel == "steam id")
                .Confidence.Should()
                .Be(42);
            survivors
                .Single(e => e.StoreId == storeId && e.RawLabel == "discord tag")
                .CanonicalLabel.Should()
                .Be("Discord Tag");
            survivors
                .Single(e => e.StoreId == otherStoreId && e.RawLabel == "steam id")
                .Confidence.Should()
                .Be(77);

            var columns = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT column_name AS "Value" FROM information_schema.columns
                    WHERE table_name = 'store_input_resolution_log'
                    """
                )
                .ToListAsync();
            columns.Should().NotContain("StoreItemId");

            var indexes = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexname AS "Value" FROM pg_indexes
                    WHERE tablename = 'store_input_resolution_log'
                    """
                )
                .ToListAsync();
            indexes.Should().Contain("IX_store_input_resolution_log_StoreId_RawLabel_ObservedAt");
        }
    }

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
