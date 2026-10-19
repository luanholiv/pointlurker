using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Identity;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Security;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .AddCustomWaitStrategy(
                    new UntilTheHostCanConnect(),
                    strategy =>
                        strategy
                            .WithInterval(TimeSpan.FromMilliseconds(250))
                            .WithTimeout(TimeSpan.FromMinutes(1))
                )
        )
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString =
            _container.GetConnectionString() + ";Maximum Pool Size=5;Minimum Pool Size=0";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IDataProtectorFactory, DataProtectorFactory>();
        foreach (
            var purpose in new[]
            {
                ProtectionPurposes.StreamElementsToken,
                ProtectionPurposes.StoreInputProfile,
                ProtectionPurposes.RedemptionMessage,
            }
        )
        {
            services.AddKeyedSingleton<ISecretProtector>(
                purpose,
                (sp, key) => sp.GetRequiredService<IDataProtectorFactory>().Create((string)key!)
            );
        }
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<IdentityDbContext>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync();
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = new[] { "public" },
                TablesToIgnore = new[] { new Table("public", "__EFMigrationsHistory") },
            }
        );
    }

    /// <summary>
    /// Holds <c>StartAsync</c> until the <em>published</em> port accepts a real connection.
    /// <para>
    /// The module's default gate runs <c>pg_isready</c> <em>inside</em> the container, which proves the
    /// server listens on the container's own loopback — not that Docker has finished forwarding the
    /// mapped host port the connection string points at. Behind a host port proxy the two can be
    /// milliseconds apart, and nothing downstream retries: the first connect is the migration, so
    /// losing that race surfaced as an immediate "Connection refused" out of <c>InitializeAsync</c>.
    /// </para>
    /// <para>
    /// Connecting from the host subsumes the in-container check — a connection that opens proves both
    /// that the server is accepting queries and that the port is forwarded.
    /// </para>
    /// </summary>
    private sealed class UntilTheHostCanConnect : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            try
            {
                await using var connection = new NpgsqlConnection(
                    ((PostgreSqlContainer)container).GetConnectionString()
                );
                await connection.OpenAsync();
                return true;
            }
            catch (Exception exception) when (exception is NpgsqlException or SocketException)
            {
                return false;
            }
        }
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
