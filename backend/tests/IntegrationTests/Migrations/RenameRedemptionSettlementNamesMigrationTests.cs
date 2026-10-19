using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the RenameRedemptionSettlementNames migration: the settle-time column takes its
/// provider-neutral name, and stored status values follow the renamed enum members both ways.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class RenameRedemptionSettlementNamesMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260929161433_AddFulfillmentCodes";

    private static readonly DateTimeOffset SettledAt = new(2026, 10, 1, 12, 30, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public RenameRedemptionSettlementNamesMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container: the fixture's main database is already at
        // the latest migration, but this test must seed rows under the earlier names.
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            Database = $"mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_renames_the_settle_time_and_the_status_values_and_keeps_every_value()
    {
        var timedOutId = Guid.NewGuid();
        var rejectedId = Guid.NewGuid();
        var completedId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            await InsertAsync(
                db,
                timedOutId,
                StoreProvider.StreamElements,
                "SECompletionTimedOut",
                null
            );
            await InsertAsync(
                db,
                rejectedId,
                StoreProvider.StreamElements,
                "SERejected",
                SettledAt
            );
            await InsertAsync(db, completedId, StoreProvider.GauPoints, "Completed", SettledAt);

            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var timedOut = await db.Redemptions.SingleAsync(r => r.Id == timedOutId);
            timedOut.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
            timedOut.SettledAt.Should().BeNull();

            var rejected = await db.Redemptions.SingleAsync(r => r.Id == rejectedId);
            rejected.Status.Should().Be(RedemptionStatus.Rejected);
            rejected.SettledAt.Should().Be(SettledAt);

            var completed = await db.Redemptions.SingleAsync(r => r.Id == completedId);
            completed.Provider.Should().Be(StoreProvider.GauPoints);
            completed.Status.Should().Be(RedemptionStatus.Completed);
            completed.SettledAt.Should().Be(SettledAt);
        }
    }

    [Fact]
    public async Task Reverting_restores_the_earlier_column_and_status_values()
    {
        var rejectedId = Guid.NewGuid();
        var timedOutId = Guid.NewGuid();

        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await InsertAsync(db, rejectedId, StoreProvider.StreamElements, "SERejected", SettledAt);
        await InsertAsync(
            db,
            timedOutId,
            StoreProvider.StreamElements,
            "SECompletionTimedOut",
            null
        );

        await migrator.MigrateAsync();
        await migrator.MigrateAsync(PreviousMigration);

        var rows = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT "Status" || '|' || coalesce(to_char("SECompletedAt" AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-')
                    AS "Value"
                FROM redemptions ORDER BY "Status"
                """
            )
            .ToListAsync();
        rows.Should().Equal("SECompletionTimedOut|-", "SERejected|2026-10-01 12:30");
    }

    private static Task<int> InsertAsync(
        AppDbContext db,
        Guid id,
        StoreProvider provider,
        string status,
        DateTimeOffset? settledAt
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO redemptions
                ("Id", "WishlistEntryId", "StoreItemId", "StoreItemName", "StoreName", "Provider",
                 "StoreExternalId", "ProviderItemId", "ProviderRedemptionId", "Status",
                 "Attempts", "AttemptedAt", "SubmittedAt", "SECompletedAt")
            VALUES ({0}, gen_random_uuid(), gen_random_uuid(), 'Item', 'Store', {1}, 'store',
                    'item-1', NULL, {2}, 1, now(), now(), {3});
            """,
            id,
            (int)provider,
            status,
            new NpgsqlParameter("settledAt", NpgsqlDbType.TimestampTz)
            {
                Value = settledAt is { } value ? value : DBNull.Value,
            }
        );

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
