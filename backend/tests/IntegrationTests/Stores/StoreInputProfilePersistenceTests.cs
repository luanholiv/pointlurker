using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class StoreInputProfilePersistenceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public StoreInputProfilePersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Jsonb_round_trip_preserves_values_and_aliases()
    {
        var ts = DateTimeOffset.UtcNow;
        var storeId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch-1"),
                "Test",
                Guid.NewGuid(),
                ts
            );
            store.UpsertValue("Twitch Username", "cipher-twitch", ts);
            store.UpsertValue("Steam ID", "cipher-steam", ts);
            store.UpsertAlias("twitchh usrname", "Twitch Username");
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var loaded = await db.WatchedStores.AsNoTracking().FirstAsync(s => s.Id == storeId);
            loaded.InputValues.Should().HaveCount(2);
            loaded.InputValues.Should().ContainSingle(v => v.LabelCanonical == "Twitch Username");
            loaded.GetCiphertext("Steam ID").Should().Be("cipher-steam");
            loaded.InputAliases.Should().ContainSingle();
            loaded.InputAliases[0].RawLabel.Should().Be("twitchh usrname");
        }
    }

    [Fact]
    public async Task Cascade_delete_removes_resolution_log_rows()
    {
        var storeId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch-cas"),
                "Test",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow
            );
            db.WatchedStores.Add(store);
            db.StoreInputResolutionLog.Add(
                new StoreInputResolutionLogEntry(
                    Guid.NewGuid(),
                    storeId,
                    "raw",
                    "Canonical",
                    95,
                    ResolutionSource.Fuzzy,
                    DateTimeOffset.UtcNow
                )
            );
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var store = await db.WatchedStores.FirstAsync(s => s.Id == storeId);
            db.WatchedStores.Remove(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var logCount = await db.StoreInputResolutionLog.CountAsync(e => e.StoreId == storeId);
            logCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task Migration_drops_legacy_user_input_values_column()
    {
        await using var db = CreateDbContext();
        var matches = await db
            .Database.SqlQueryRaw<int>(
                @"SELECT 1 AS ""Value"" FROM information_schema.columns
                  WHERE table_name = 'wishlist_entries' AND column_name = 'user_input_values_legacy'"
            )
            .ToListAsync();
        matches.Should().BeEmpty();
    }

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
