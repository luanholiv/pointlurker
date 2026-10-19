using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the AddProviderCredentials migration, the expand step that moves provider secrets off
/// the user row. It must stay additive — the release before it keeps reading the token from
/// <c>AspNetUsers</c> after a rollback — and it must carry the existing token across, or upgrading
/// would silently sign the owner out of StreamElements.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class AddProviderCredentialsMigrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260602180936_AddSeUsername";
    private const string ExpandMigration = "20260929124719_AddProviderCredentials";

    private static readonly DateTimeOffset TokenSetAt = new(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
    private static readonly DateTimeOffset UsernameRefreshedAt = new(
        2026,
        6,
        7,
        8,
        9,
        10,
        TimeSpan.Zero
    );

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public AddProviderCredentialsMigrationTests(DatabaseFixture fixture)
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
    public async Task Migration_removes_no_user_column_the_previous_release_reads_or_writes()
    {
        IReadOnlyList<string> before;
        IReadOnlyList<string> after;

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            before = await ColumnsAsync(db, "AspNetUsers");

            await migrator.MigrateAsync(ExpandMigration);
            after = await ColumnsAsync(db, "AspNetUsers");
        }

        after.Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task The_stored_StreamElements_token_and_username_are_copied_across()
    {
        var owner = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertUserAsync(db, owner, "ciphertext", TokenSetAt, "owner-se");
            await migrator.MigrateAsync(ExpandMigration);
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db
                .ProviderCredentials.Select(CredentialSnapshot.Project)
                .SingleAsync();

            credential.UserId.Should().Be(owner);
            credential.Provider.Should().Be(StoreProvider.StreamElements);
            credential.EncryptedSecret.Should().Be("ciphertext");
            credential.SecretSetAt.Should().Be(TokenSetAt);
            credential.AccountName.Should().Be("owner-se");
            credential.AccountNameRefreshedAt.Should().Be(UsernameRefreshedAt);
            credential.LastValidatedAt.Should().BeNull();
            credential.LastValidationError.Should().BeNull();

            // Copied, not moved: the previous release still reads the token from here. Read raw,
            // since the current model no longer maps these columns.
            var token = await db
                .Database.SqlQuery<string>(
                    $"""SELECT "StreamElementsToken" AS "Value" FROM "AspNetUsers" """
                )
                .SingleAsync();
            token.Should().Be("ciphertext");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_user_with_no_token_gets_no_credential(string? token)
    {
        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await InsertUserAsync(db, Guid.NewGuid(), token, TokenSetAt, "owner-se");

        await migrator.MigrateAsync(ExpandMigration);

        (await db.ProviderCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_token_with_no_timestamp_or_username_still_copies()
    {
        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);
            await InsertUserAsync(db, Guid.NewGuid(), "ciphertext", setAt: null, username: " ");
            await migrator.MigrateAsync(ExpandMigration);
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db
                .ProviderCredentials.Select(CredentialSnapshot.Project)
                .SingleAsync();

            credential.SecretSetAt.Should().NotBe(default);
            credential.AccountName.Should().BeNull();
            credential.AccountNameRefreshedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task A_user_holds_one_credential_per_provider()
    {
        var owner = Guid.NewGuid();

        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await InsertUserAsync(db, owner, "ciphertext", TokenSetAt, "owner-se");
        await migrator.MigrateAsync(ExpandMigration);

        // Another provider is another credential...
        var inserted = await InsertCredentialAsync(db, owner, StoreProvider.GauPoints);
        inserted.Should().Be(1);

        // ...but the migrated StreamElements one already fills its slot.
        var duplicate = () => InsertCredentialAsync(db, owner, StoreProvider.StreamElements);
        await duplicate
            .Should()
            .ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    /// <summary>A user row written with the columns the previous release knew.</summary>
    private static Task<int> InsertUserAsync(
        IdentityDbContext db,
        Guid id,
        string? token,
        DateTimeOffset? setAt,
        string? username
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetUsers"
                ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
                 "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount",
                 "StreamElementsToken", "StreamElementsTokenSetAt", "StreamElementsUsername",
                 "StreamElementsUsernameRefreshedAt")
            VALUES (@id, 'owner@example.com', 'OWNER@EXAMPLE.COM', 'owner@example.com',
                    'OWNER@EXAMPLE.COM', true, false, false, false, 0, @token, @setAt, @username,
                    @refreshedAt);
            """,
            // Typed parameters: a null has no CLR type to infer a column type from.
            new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = id },
            new NpgsqlParameter("token", NpgsqlDbType.Varchar) { Value = Nullable(token) },
            new NpgsqlParameter("setAt", NpgsqlDbType.TimestampTz) { Value = Nullable(setAt) },
            new NpgsqlParameter("username", NpgsqlDbType.Text) { Value = Nullable(username) },
            new NpgsqlParameter("refreshedAt", NpgsqlDbType.TimestampTz)
            {
                Value = UsernameRefreshedAt,
            }
        );

    private static object Nullable(object? value) => value ?? DBNull.Value;

    private static Task<int> InsertCredentialAsync(
        IdentityDbContext db,
        Guid userId,
        StoreProvider provider
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ProviderCredentials" ("UserId", "Provider", "EncryptedSecret", "SecretSetAt")
            VALUES ({0}, {1}, 'other-ciphertext', now());
            """,
            userId,
            (int)provider
        );

    private static async Task<IReadOnlyList<string>> ColumnsAsync(
        IdentityDbContext db,
        string table
    ) =>
        await db
            .Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = {table}
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
