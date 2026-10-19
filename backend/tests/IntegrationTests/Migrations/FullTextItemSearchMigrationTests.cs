using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the AddFullTextItemSearch migration. Three of the four things it creates are invisible
/// to the model snapshot — the text search configuration, its accent-folding mapping, and an index
/// over expressions rather than columns — so nothing else in the build would notice if they stopped
/// being created. The item search silently degrades to matching nothing rather than failing, which
/// is the failure this exists to catch.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class FullTextItemSearchMigrationTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private string _connectionString = null!;

    public Task InitializeAsync()
    {
        // A dedicated database on the shared container, so applying and reverting the migration
        // cannot disturb the fixture database every other test in the collection shares.
        var builder = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"fts_mig_test_{Guid.NewGuid():N}",
        };
        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_creates_the_extensions_configuration_and_index()
    {
        await using (var db = CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var extensions = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT extname AS "Value" FROM pg_extension
                    """
                )
                .ToListAsync();

            extensions.Should().Contain(["unaccent", "pg_trgm"]);

            var configurations = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT cfgname AS "Value" FROM pg_ts_config
                    """
                )
                .ToListAsync();

            configurations.Should().Contain("item_search");

            var indexes = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexname AS "Value" FROM pg_indexes WHERE tablename = 'store_items'
                    """
                )
                .ToListAsync();

            indexes.Should().Contain("IX_store_items_Search");

            // The index refers to the configuration by OID, so after the rename its definition must
            // name item_search — the expression the repository builds — for the planner to use it.
            var definition = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexdef AS "Value" FROM pg_indexes WHERE indexname = 'IX_store_items_Search'
                    """
                )
                .SingleAsync();

            definition.Should().Contain("'item_search'::regconfig").And.NotContain("se_search");
        }
    }

    /// <summary>
    /// The configuration is only useful if it actually folds accents, which is a property of the
    /// mapping rather than of the configuration existing. Asserted through the database because the
    /// dictionary is what does the folding — no amount of C# can stand in for it.
    /// </summary>
    [Fact]
    public async Task The_configuration_folds_accents_in_both_directions()
    {
        await using (var db = CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var matches = await db
                .Database.SqlQuery<bool>(
                    $"""
                    SELECT (
                        to_tsvector('item_search', 'Doação') @@ websearch_to_tsquery('item_search', 'doacao')
                        AND to_tsvector('item_search', 'INVENTARIO') @@ websearch_to_tsquery('item_search', 'inventário')
                    ) AS "Value"
                    """
                )
                .SingleAsync();

            matches.Should().BeTrue();
        }
    }

    /// <summary>
    /// Down has to undo its own dependency chain — the index is built on the configuration and the
    /// configuration on the dictionary — so reverting is where a wrong order surfaces.
    /// </summary>
    [Fact]
    public async Task Migration_reverts_cleanly()
    {
        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync();
            await migrator.MigrateAsync(PreviousMigration);
        }

        await using (var db = CreateDbContext())
        {
            var configurations = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT cfgname AS "Value" FROM pg_ts_config
                    """
                )
                .ToListAsync();

            configurations.Should().NotContain(["se_search", "item_search"]);

            var indexes = await db
                .Database.SqlQuery<string>(
                    $"""
                    SELECT indexname AS "Value" FROM pg_indexes WHERE tablename = 'store_items'
                    """
                )
                .ToListAsync();

            indexes.Should().NotContain("IX_store_items_Search");
        }
    }

    private const string PreviousMigration = "20260821143135_RenameLastFetchedAtToLastChangedAt";

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
