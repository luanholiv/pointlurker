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
/// Verifies DropUserStreamElementsColumns, the contract step of the credential move. It drops the
/// write-only mirror on <c>AspNetUsers</c>, and before it does, carries across any token the mirror
/// alone holds — one pasted while rolled back to a release that predates credentials.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class DropUserStreamElementsColumnsMigrationTests : IAsyncLifetime
{
    private const string ExpandMigration = "20260929124719_AddProviderCredentials";
    private const string ContractMigration = "20260929133427_DropUserStreamElementsColumns";

    private static readonly DateTimeOffset DualWriteAt = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RolledBackAt = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidatedAt = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] MirrorColumns =
    [
        "StreamElementsToken",
        "StreamElementsTokenSetAt",
        "StreamElementsUsername",
        "StreamElementsUsernameRefreshedAt",
    ];

    private readonly DatabaseFixture _fixture;
    private string _connectionString = null!;

    public DropUserStreamElementsColumnsMigrationTests(DatabaseFixture fixture)
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
    public async Task Drops_the_four_mirror_columns_and_nothing_else()
    {
        IReadOnlyList<string> usersBefore;
        IReadOnlyList<string> usersAfter;
        IReadOnlyList<string> credentialsBefore;
        IReadOnlyList<string> credentialsAfter;

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);
            usersBefore = await ColumnsAsync(db, "AspNetUsers");
            credentialsBefore = await ColumnsAsync(db, "ProviderCredentials");

            await migrator.MigrateAsync(ContractMigration);
            usersAfter = await ColumnsAsync(db, "AspNetUsers");
            credentialsAfter = await ColumnsAsync(db, "ProviderCredentials");
        }

        usersBefore.Except(usersAfter).Should().BeEquivalentTo(MirrorColumns);
        usersAfter.Should().BeSubsetOf(usersBefore);
        credentialsAfter.Should().BeEquivalentTo(credentialsBefore);
    }

    [Fact]
    public async Task A_credential_kept_in_step_by_the_dual_write_is_left_as_it_is()
    {
        var owner = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);
            await InsertUserAsync(db, owner, "ciphertext", DualWriteAt, "owner-se");
            await InsertCredentialAsync(db, owner, "ciphertext", DualWriteAt, "owner-se");
            await migrator.MigrateAsync(ContractMigration);
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db
                .ProviderCredentials.Select(CredentialSnapshot.Project)
                .SingleAsync();

            credential.EncryptedSecret.Should().Be("ciphertext");
            credential.SecretSetAt.Should().Be(DualWriteAt);
            credential.AccountName.Should().Be("owner-se");
            credential.LastValidatedAt.Should().Be(ValidatedAt);
        }
    }

    [Fact]
    public async Task A_token_pasted_while_rolled_back_replaces_the_older_credential()
    {
        var owner = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);
            await InsertCredentialAsync(
                db,
                owner,
                "old-ciphertext",
                DualWriteAt,
                "owner-se",
                insertUser: true
            );
            await UpdateUserTokenAsync(
                db,
                owner,
                "rolled-back-ciphertext",
                RolledBackAt,
                "renamed-se"
            );
            await migrator.MigrateAsync(ContractMigration);
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db
                .ProviderCredentials.Select(CredentialSnapshot.Project)
                .SingleAsync();

            credential.EncryptedSecret.Should().Be("rolled-back-ciphertext");
            credential.SecretSetAt.Should().Be(RolledBackAt);
            credential.AccountName.Should().Be("renamed-se");

            // A different secret: whatever was known about the old one does not carry over.
            credential.LastValidatedAt.Should().BeNull();
            credential.LastValidationError.Should().BeNull();
        }
    }

    [Fact]
    public async Task A_first_token_pasted_while_rolled_back_becomes_a_credential()
    {
        var owner = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(ExpandMigration);
            await InsertUserAsync(db, owner, "rolled-back-ciphertext", RolledBackAt, "owner-se");
            await migrator.MigrateAsync(ContractMigration);
        }

        await using (var db = CreateDbContext())
        {
            var credential = await db
                .ProviderCredentials.Select(CredentialSnapshot.Project)
                .SingleAsync();

            credential.UserId.Should().Be(owner);
            credential.Provider.Should().Be(StoreProvider.StreamElements);
            credential.EncryptedSecret.Should().Be("rolled-back-ciphertext");
            credential.AccountName.Should().Be("owner-se");
        }
    }

    [Fact]
    public async Task A_user_with_no_token_and_no_credential_gets_none()
    {
        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(ExpandMigration);
        await InsertUserAsync(db, Guid.NewGuid(), token: null, setAt: null, username: null);

        await migrator.MigrateAsync(ContractMigration);

        (await db.ProviderCredentials.CountAsync()).Should().Be(0);
    }

    /// <summary>A user row written with the mirror columns the expand release still carried.</summary>
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
                    @setAt);
            """,
            // Typed parameters: a null has no CLR type to infer a column type from.
            new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = id },
            new NpgsqlParameter("token", NpgsqlDbType.Varchar) { Value = Nullable(token) },
            new NpgsqlParameter("setAt", NpgsqlDbType.TimestampTz) { Value = Nullable(setAt) },
            new NpgsqlParameter("username", NpgsqlDbType.Text) { Value = Nullable(username) }
        );

    /// <summary>What the previous release wrote on a paste: the user row only.</summary>
    private static Task<int> UpdateUserTokenAsync(
        IdentityDbContext db,
        Guid id,
        string token,
        DateTimeOffset setAt,
        string username
    ) =>
        db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "AspNetUsers"
            SET "StreamElementsToken" = {1}, "StreamElementsTokenSetAt" = {2},
                "StreamElementsUsername" = {3}, "StreamElementsUsernameRefreshedAt" = {2}
            WHERE "Id" = {0};
            """,
            id,
            token,
            setAt,
            username
        );

    /// <summary>A validated credential, as the expand release's dual write left it.</summary>
    private static async Task InsertCredentialAsync(
        IdentityDbContext db,
        Guid userId,
        string ciphertext,
        DateTimeOffset setAt,
        string accountName,
        bool insertUser = false
    )
    {
        if (insertUser)
            await InsertUserAsync(db, userId, ciphertext, setAt, accountName);

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "ProviderCredentials"
                ("UserId", "Provider", "EncryptedSecret", "SecretSetAt", "AccountName",
                 "AccountNameRefreshedAt", "LastValidatedAt")
            VALUES ({0}, 0, {1}, {2}, {3}, {2}, {4});
            """,
            userId,
            ciphertext,
            setAt,
            accountName,
            ValidatedAt
        );
    }

    private static object Nullable(object? value) => value ?? DBNull.Value;

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
