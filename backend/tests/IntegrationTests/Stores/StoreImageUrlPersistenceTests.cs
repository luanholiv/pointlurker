using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class StoreImageUrlPersistenceTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;

    public StoreImageUrlPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Image_urls_round_trip()
    {
        var ts = DateTimeOffset.UtcNow;
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch-img"),
                "Test",
                Guid.NewGuid(),
                ts,
                avatarUrl: "https://cdn.example.com/avatar.png"
            );
            store.AddItem(
                new StoreItem(
                    itemId,
                    storeId,
                    CreateApiData(
                        ts,
                        thumbnailUrl: "https://cdn.example.com/thumb.png",
                        previewUrl: "https://cdn.example.com/preview.png"
                    )
                )
            );
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var store = await db.WatchedStores.AsNoTracking().FirstAsync(s => s.Id == storeId);
            store.AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");

            var item = await db.StoreItems.AsNoTracking().FirstAsync(i => i.Id == itemId);
            item.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.png");
            item.PreviewUrl.Should().Be("https://cdn.example.com/preview.png");
        }
    }

    [Fact]
    public async Task Null_image_urls_round_trip()
    {
        var ts = DateTimeOffset.UtcNow;
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            var store = new WatchedStore(
                storeId,
                StoreProvider.StreamElements,
                new StoreExternalId("ch-null"),
                "Test",
                Guid.NewGuid(),
                ts
            );
            store.AddItem(new StoreItem(itemId, storeId, CreateApiData(ts)));
            db.WatchedStores.Add(store);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var store = await db.WatchedStores.AsNoTracking().FirstAsync(s => s.Id == storeId);
            store.AvatarUrl.Should().BeNull();

            var item = await db.StoreItems.AsNoTracking().FirstAsync(i => i.Id == itemId);
            item.ThumbnailUrl.Should().BeNull();
            item.PreviewUrl.Should().BeNull();
        }
    }

    [Fact]
    public async Task Migration_creates_image_url_columns()
    {
        await using var db = CreateDbContext();
        var columns = await db
            .Database.SqlQueryRaw<string>(
                @"SELECT column_name AS ""Value"" FROM information_schema.columns
                  WHERE (table_name = 'store_items' AND column_name IN ('ThumbnailUrl', 'PreviewUrl'))
                     OR (table_name = 'watched_stores' AND column_name = 'AvatarUrl')"
            )
            .ToListAsync();

        columns.Should().BeEquivalentTo("ThumbnailUrl", "PreviewUrl", "AvatarUrl");
    }

    private static StoreItemApiData CreateApiData(
        DateTimeOffset ts,
        string? thumbnailUrl = null,
        string? previewUrl = null
    ) =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId("se-img-1"),
            new StoreExternalId("ch-img"),
            "Item",
            null,
            "perk",
            100,
            "default",
            true,
            false,
            true,
            false,
            false,
            [],
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            ts,
            null,
            ts,
            thumbnailUrl,
            previewUrl
        );

    private AppDbContext CreateDbContext()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AppDbContext>();
    }
}
