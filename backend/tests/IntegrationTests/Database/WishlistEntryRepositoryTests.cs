using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Database;

[Collection(ContainersCollection.Name)]
public sealed class WishlistEntryRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private AppDbContext _db = null!;
    private WishlistEntryRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        _db = new AppDbContext(options);
        _sut = new WishlistEntryRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ListAutoRedeemWithItemsForUpdateAsync_filters_by_AutoRedeem_true()
    {
        var userId = Guid.NewGuid();
        var (_, item1) = await SeedItem();
        var (_, item2) = await SeedItem();

        var on = new WishlistEntry(Guid.NewGuid(), userId, item1.Id, Now);
        on.EnableAutoRedeem(Now);

        var off = new WishlistEntry(Guid.NewGuid(), userId, item2.Id, Now);

        _db.WishlistEntries.AddRange(on, off);
        await _db.SaveChangesAsync();

        var result = await _sut.ListAutoRedeemWithItemsForUpdateAsync(userId);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(on.Id);
    }

    [Fact]
    public async Task ListAutoRedeemWithItemsForUpdateAsync_filters_by_user()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var (_, itemA) = await SeedItem();
        var (_, itemB) = await SeedItem();

        var entryA = new WishlistEntry(Guid.NewGuid(), userA, itemA.Id, Now);
        entryA.EnableAutoRedeem(Now);
        var entryB = new WishlistEntry(Guid.NewGuid(), userB, itemB.Id, Now);
        entryB.EnableAutoRedeem(Now);

        _db.WishlistEntries.AddRange(entryA, entryB);
        await _db.SaveChangesAsync();

        var result = await _sut.ListAutoRedeemWithItemsForUpdateAsync(userA);

        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(entryA.Id);
    }

    [Fact]
    public async Task ListAutoRedeemWithItemsForUpdateAsync_eager_loads_StoreItem_and_WatchedStore()
    {
        var userId = Guid.NewGuid();
        var (store, item) = await SeedItem();

        var entry = new WishlistEntry(Guid.NewGuid(), userId, item.Id, Now);
        entry.EnableAutoRedeem(Now);
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var result = await _sut.ListAutoRedeemWithItemsForUpdateAsync(userId);

        var loaded = result.Single();
        loaded.StoreItem.Should().NotBeNull();
        loaded.StoreItem!.Id.Should().Be(item.Id);
        loaded.StoreItem!.WatchedStore.Should().NotBeNull();
        loaded.StoreItem!.WatchedStore!.DisplayName.Should().Be(store.DisplayName);
    }

    [Fact]
    public async Task ListAutoRedeemWithItemsForUpdateAsync_returns_empty_when_no_matches()
    {
        var result = await _sut.ListAutoRedeemWithItemsForUpdateAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    private async Task<(WatchedStore Store, StoreItem Item)> SeedItem()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-" + Guid.NewGuid().ToString("N")[..8]),
            "Display " + Guid.NewGuid().ToString("N")[..6],
            Guid.NewGuid(),
            Now
        );
        _db.WatchedStores.Add(store);

        var item = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-" + Guid.NewGuid().ToString("N")),
                store.ExternalId,
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
                Now,
                null,
                Now
            )
        );
        item.ClearDomainEvents();
        _db.StoreItems.Add(item);
        await _db.SaveChangesAsync();

        return (store, item);
    }
}
