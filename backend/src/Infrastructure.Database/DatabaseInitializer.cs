using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PointLurker.Infrastructure.Database;

[ExcludeFromCodeCoverage]
public sealed class DatabaseInitializer(
    AppDbContext appDbContext,
    IdentityDbContext identityDbContext,
    ILogger<DatabaseInitializer> logger
) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogApplyingMigrations();

        await ApplyMigrationsAsync(identityDbContext, "Identity", cancellationToken);
        await ApplyMigrationsAsync(appDbContext, "Stores", cancellationToken);

        logger.LogMigrationsCompleted();
    }

    // Migrations may include one-time data-rewrite steps over large tables
    // (e.g. the resolution-log backlog cleanup) that take far longer than the
    // default 30s command timeout. This timeout is scoped to the initializer's
    // context instance only — runtime request contexts keep their default.
    private static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(15);

    private static async Task ApplyMigrationsAsync(
        DbContext context,
        string contextName,
        CancellationToken cancellationToken
    )
    {
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        if (pendingMigrations.Any())
        {
            context.Database.SetCommandTimeout(MigrationCommandTimeout);
            await context.Database.MigrateAsync(cancellationToken);
            return;
        }

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var hasMigrations = appliedMigrations.Any();

        if (!hasMigrations)
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
