using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies AddProviderCredentialExpiryWarning, an expand step: it only adds a nullable column, so
/// the release before it keeps reading and writing credentials after a rollback, and a credential
/// it finds is one never warned about — at worst one extra warning, never a missed one.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class AddProviderCredentialExpiryWarningMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260929144859_AddProviderCredentialExpiresAt";
    private const string ExpandMigration = "20260929172353_AddProviderCredentialExpiryWarning";

    private static readonly DateTimeOffset SetAt = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public AddProviderCredentialExpiryWarningMigrationTests(DatabaseFixture fixture)
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
    public async Task Adds_only_the_warning_marker_column()
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

        after.Except(before).Should().BeEquivalentTo(["ExpiryWarningSentFor"]);
        before.Should().BeSubsetOf(after);
    }

    [Fact]
    public async Task An_expiring_credential_written_the_previous_way_still_needs_its_warning()
    {
        var owner = Guid.NewGuid();
        var expiresAt = SetAt.AddDays(7);

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "AspNetUsers"
                    ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
                     "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled",
                     "LockoutEnabled", "AccessFailedCount", "Locale")
                VALUES ({0}, 'owner@example.com', 'OWNER@EXAMPLE.COM', 'owner@example.com',
                        'OWNER@EXAMPLE.COM', true, false, false, false, 0, 'en-US');
                INSERT INTO "ProviderCredentials"
                    ("UserId", "Provider", "EncryptedSecret", "SecretSetAt", "ExpiresAt")
                VALUES ({0}, 1, 'ciphertext', {1}, {2});
                """,
                owner,
                SetAt,
                expiresAt
            );
            await migrator.MigrateAsync(ExpandMigration);

            // The read goes through the current entity model, which maps every later column too.
            await migrator.MigrateAsync();
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db.ProviderCredentials.SingleAsync();

            credential.ExpiryWarningSentFor.Should().BeNull();
            credential
                .NeedsExpiryWarning(expiresAt.AddDays(-1), TimeSpan.FromDays(2))
                .Should()
                .BeTrue();
        }
    }

    private static async Task<IReadOnlyList<string>> ColumnsAsync(IdentityDbContext db) =>
        await db
            .Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'ProviderCredentials'
                """
            )
            .ToListAsync();

    private IdentityDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(o => o.UseNpgsql(_connectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IdentityDbContext>();
    }
}
