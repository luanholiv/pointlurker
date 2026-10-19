using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Database;

/// <summary>
/// The repositories' write contract: a write is the change tracker's deltas, so its caller must have
/// loaded the entity through a <c>*ForUpdateAsync</c> read. Three halves are worth pinning — that a
/// delta really is a delta (only the mutated columns are marked modified, unlike <c>DbSet.Update</c>,
/// which marks all of them), that a detached entity fails loudly instead of saving nothing, and that
/// the repositories only ever stage: nothing reaches the database until <see cref="IUnitOfWork"/>
/// commits.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class TrackedWriteContractTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private AppDbContext _db = null!;
    private WatchedStoreRepository _stores = null!;
    private WishlistEntryRepository _entries = null!;
    private RedemptionRepository _redemptions = null!;
    private StoreItemRepository _items = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        _db = new AppDbContext(options);
        _stores = new WatchedStoreRepository(_db);
        _entries = new WishlistEntryRepository(_db);
        _redemptions = new RedemptionRepository(_db);
        _items = new StoreItemRepository(_db);
        _unitOfWork = new UnitOfWork(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task WatchedStore_UpdateAsync_persists_a_mutation_made_after_GetForUpdateAsync()
    {
        var store = await SeedStore();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        loaded!.Deactivate();
        _stores.Update(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var reloaded = await _db.WatchedStores.SingleAsync(s => s.Id == store.Id);
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task WatchedStore_UpdateAsync_marks_only_the_mutated_column_as_modified()
    {
        var store = await SeedStore();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        loaded!.RecordSync(Now.AddHours(1));

        ModifiedProperties(loaded).Should().Equal(nameof(WatchedStore.LastSyncedAt));
    }

    [Fact]
    public async Task WatchedStore_UpdateAsync_detects_a_jsonb_collection_edit()
    {
        // The input profile lives in jsonb columns mapped from backing fields. Delta saves only see
        // an in-place edit through the value comparer, so this is the guard on that wiring.
        var store = await SeedStore();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        loaded!.UpsertValue("minecraft_username", "ciphertext", Now);
        loaded.UpsertAlias("Minecraft Username", "minecraft_username");

        ModifiedProperties(loaded)
            .Should()
            .Equal(nameof(WatchedStore.InputAliases), nameof(WatchedStore.InputValues));

        _stores.Update(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var reloaded = await _db.WatchedStores.SingleAsync(s => s.Id == store.Id);
        reloaded.GetCiphertext("minecraft_username").Should().Be("ciphertext");
        reloaded.InputAliases.Should().ContainSingle();
    }

    [Fact]
    public async Task WatchedStore_UpdateAsync_rejects_a_detached_store()
    {
        var store = await SeedStore();
        _db.ChangeTracker.Clear();

        var detached = await _stores.GetProfileAsync(store.Id);
        detached!.Deactivate();

        var act = () => _stores.Update(detached);

        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains(nameof(WatchedStore), StringComparison.Ordinal))
            .Where(ex => ex.Message.Contains("GetForUpdateAsync", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WatchedStore_GetForUpdateAsync_returns_null_for_an_unknown_id()
    {
        (await _stores.GetForUpdateAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task WatchedStore_DeleteAsync_cascades_to_items_without_loading_them()
    {
        // RemoveStore now deletes a row-only tracked instance, so the children go by the database's
        // ON DELETE CASCADE rather than by EF deleting a graph it had to load first.
        var store = await SeedStore();
        var item = await SeedItem(store);
        _db.ChangeTracker.Clear();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        loaded!.Items.Should().BeEmpty();
        _stores.Remove(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        (await _db.StoreItems.AnyAsync(i => i.Id == item.Id)).Should().BeFalse();
        (await _db.WatchedStores.AnyAsync(s => s.Id == store.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task WishlistEntry_UpdateAsync_marks_only_the_mutated_columns_as_modified()
    {
        var entry = await SeedEntry();

        var loaded = await _entries.GetForUpdateAsync(entry.Id);
        loaded!.BlockOnMissingInput(Now.AddHours(1));

        ModifiedProperties(loaded)
            .Should()
            .Equal(
                nameof(WishlistEntry.AutoRedeem),
                nameof(WishlistEntry.IsBlockedOnInput),
                nameof(WishlistEntry.UpdatedAt)
            );

        _entries.Update(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var reloaded = await _db.WishlistEntries.SingleAsync(e => e.Id == entry.Id);
        reloaded.IsBlockedOnInput.Should().BeTrue();
    }

    [Fact]
    public async Task WishlistEntry_UpdateAsync_rejects_a_detached_entry()
    {
        var entry = await SeedEntry();
        _db.ChangeTracker.Clear();

        var detached = await _entries.GetByIdAsync(entry.Id);
        detached!.BlockOnMissingInput(Now);

        var act = () => _entries.Update(detached);

        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("GetForUpdateAsync", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Redemption_UpdateAsync_marks_only_the_mutated_columns_as_modified()
    {
        var redemption = await SeedRedemption();
        _db.ChangeTracker.Clear();

        var pending = await _redemptions.GetPendingForUpdateAsync(Now.AddMinutes(1));
        var loaded = pending.Single(r => r.Id == redemption.Id);
        loaded.MarkSubmitted("se-redemption-1", Now.AddMinutes(1));

        ModifiedProperties(loaded)
            .Should()
            .Equal(
                nameof(Redemption.Attempts),
                nameof(Redemption.ProviderRedemptionId),
                nameof(Redemption.Status),
                nameof(Redemption.SubmittedAt)
            );

        _redemptions.Update(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemption.Id);
        reloaded.Status.Should().Be(RedemptionStatus.Submitted);
        reloaded.ProviderRedemptionId.Should().Be("se-redemption-1");
    }

    [Fact]
    public async Task Redemption_GetSubmittedForUpdateAsync_returns_tracked_rows()
    {
        var redemption = await SeedRedemption();
        var seeded = await _redemptions.GetPendingForUpdateAsync(Now.AddMinutes(1));
        seeded.Single(r => r.Id == redemption.Id).MarkSubmitted("se-1", Now);
        _redemptions.Update(seeded.Single(r => r.Id == redemption.Id));
        await _unitOfWork.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var submitted = await _redemptions.GetSubmittedForUpdateAsync();
        var loaded = submitted.Single(r => r.Id == redemption.Id);
        loaded.MarkCompleted(Now.AddMinutes(2));
        _redemptions.Update(loaded);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemption.Id);
        reloaded.Status.Should().Be(RedemptionStatus.Completed);
    }

    [Fact]
    public async Task Redemption_UpdateAsync_rejects_a_detached_redemption()
    {
        var redemption = await SeedRedemption();
        _db.ChangeTracker.Clear();

        // The context default is NoTracking, so a plain query after Clear() hands back a detached row
        // — exactly the shape a caller reaching for a read method by mistake would get.
        var detached = await _db.Redemptions.SingleAsync(r => r.Id == redemption.Id);
        detached.MarkSubmitted("se-redemption-1", Now.AddMinutes(1));

        var act = () => _redemptions.Update(detached);

        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains(nameof(Redemption), StringComparison.Ordinal));
    }

    [Fact]
    public async Task StoreItem_UpsertAsync_inserts_untracked_items_and_delta_saves_tracked_ones()
    {
        var store = await SeedStore();
        var existing = await SeedItem(store);
        _db.ChangeTracker.Clear();

        var tracked = await _items.ListByStoreForUpdateAsync(store.Id);
        var loaded = tracked.Single();
        loaded.UpdateFromApi(ApiData(store, loaded.ProviderItemId, name: "Renamed", cost: 250));

        var inserted = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            ApiData(store, new ProviderItemId("se-" + Guid.NewGuid().ToString("N")))
        );
        inserted.ClearDomainEvents();

        _items.Upsert([loaded, inserted]);
        await _unitOfWork.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var all = await _db.StoreItems.Where(i => i.WatchedStoreId == store.Id).ToListAsync();
        all.Should().HaveCount(2);
        all.Single(i => i.Id == existing.Id).Name.Should().Be("Renamed");
        all.Single(i => i.Id == existing.Id).Cost.Should().Be(250);
        all.Should().ContainSingle(i => i.Id == inserted.Id);
    }

    [Fact]
    public async Task StoreItem_UpsertAsync_marks_only_the_mutated_columns_as_modified()
    {
        var store = await SeedStore();
        await SeedItem(store);
        _db.ChangeTracker.Clear();

        var tracked = await _items.ListByStoreForUpdateAsync(store.Id);
        var loaded = tracked.Single();
        loaded.UpdateFromApi(ApiData(store, loaded.ProviderItemId, name: "Renamed"));

        // UpdateFromApi assigns every field from the payload, so the claim worth pinning is not an
        // exact set but that unchanged assignments produce no write — which is precisely what
        // DbSet.Update could never do.
        var modified = ModifiedProperties(loaded);
        modified.Should().Contain(nameof(StoreItem.Name));
        modified
            .Should()
            .NotContain(nameof(StoreItem.StoreExternalId))
            .And.NotContain(nameof(StoreItem.ProviderItemId))
            .And.NotContain(nameof(StoreItem.CategoryName))
            .And.NotContain(nameof(StoreItem.Type))
            .And.NotContain(nameof(StoreItem.Cost));
    }

    [Fact]
    public async Task Add_stages_the_row_and_writes_nothing_until_the_unit_of_work_commits()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-" + Guid.NewGuid().ToString("N")[..8]),
            "Staged",
            Guid.NewGuid(),
            Now
        );

        _stores.Add(store);

        await using (var reader = NewContext())
        {
            (await reader.WatchedStores.AnyAsync(s => s.Id == store.Id)).Should().BeFalse();
        }

        await _unitOfWork.SaveChangesAsync();

        await using var after = NewContext();
        (await after.WatchedStores.AnyAsync(s => s.Id == store.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Update_stages_the_mutation_and_writes_nothing_until_the_unit_of_work_commits()
    {
        var store = await SeedStore();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        loaded!.Deactivate();
        _stores.Update(loaded);

        await using (var reader = NewContext())
        {
            (await reader.WatchedStores.SingleAsync(s => s.Id == store.Id))
                .IsActive.Should()
                .BeTrue();
        }

        await _unitOfWork.SaveChangesAsync();

        await using var after = NewContext();
        (await after.WatchedStores.SingleAsync(s => s.Id == store.Id)).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_stages_the_delete_and_writes_nothing_until_the_unit_of_work_commits()
    {
        var store = await SeedStore();

        var loaded = await _stores.GetForUpdateAsync(store.Id);
        _stores.Remove(loaded!);

        await using (var reader = NewContext())
        {
            (await reader.WatchedStores.AnyAsync(s => s.Id == store.Id)).Should().BeTrue();
        }

        await _unitOfWork.SaveChangesAsync();

        await using var after = NewContext();
        (await after.WatchedStores.AnyAsync(s => s.Id == store.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task One_commit_persists_every_staged_change_across_repositories()
    {
        // The point of the unit of work: two aggregates staged through two repositories, one
        // transaction rather than one commit per repository call.
        var store = await SeedStore();
        var item = await SeedItem(store);
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        entry.EnableAutoRedeem(Now);
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var trackedStore = await _stores.GetForUpdateAsync(store.Id);
        trackedStore!.Deactivate();
        _stores.Update(trackedStore);

        var trackedEntry = await _entries.GetForUpdateAsync(entry.Id);
        trackedEntry!.BlockOnMissingInput(Now.AddHours(1));
        _entries.Update(trackedEntry);

        await _unitOfWork.SaveChangesAsync();

        await using var after = NewContext();
        (await after.WatchedStores.SingleAsync(s => s.Id == store.Id)).IsActive.Should().BeFalse();
        (await after.WishlistEntries.SingleAsync(e => e.Id == entry.Id))
            .IsBlockedOnInput.Should()
            .BeTrue();
    }

    /// <summary>
    /// A second context on the same database, so a read cannot be served from the change tracker the
    /// staged write lives in. Reading through <c>_db</c> would prove nothing about what was committed.
    /// </summary>
    private AppDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options
        );

    /// <summary>
    /// The property names EF would write for <paramref name="entity"/> right now, sorted so the
    /// assertion does not depend on the change tracker's ordering. An empty result means the save
    /// would be a no-op; the full property list means the write is still a whole-row write.
    /// </summary>
    private IReadOnlyList<string> ModifiedProperties<TEntity>(TEntity entity)
        where TEntity : class =>
        _db.Entry(entity)
            .Properties.Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private async Task<WatchedStore> SeedStore()
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
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return store;
    }

    private async Task<StoreItem> SeedItem(WatchedStore store)
    {
        var item = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            ApiData(store, new ProviderItemId("se-" + Guid.NewGuid().ToString("N")))
        );
        item.ClearDomainEvents();
        _db.StoreItems.Add(item);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return item;
    }

    private async Task<WishlistEntry> SeedEntry()
    {
        var store = await SeedStore();
        var item = await SeedItem(store);
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        // Auto-redeem on, so BlockOnMissingInput has all three of its writes to make — a seed with it
        // off would leave AutoRedeem=false unchanged and quietly weaken the delta assertion.
        entry.EnableAutoRedeem(Now);
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return entry;
    }

    private async Task<Redemption> SeedRedemption()
    {
        var store = await SeedStore();
        var item = await SeedItem(store);
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        entry.EnableAutoRedeem(Now);
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var redemption = entry.CreateRedemption(item, null, item.Name, store.DisplayName, Now);
        _db.Redemptions.Add(redemption);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return redemption;
    }

    private static StoreItemApiData ApiData(
        WatchedStore store,
        ProviderItemId seId,
        string name = "Item",
        int cost = 100
    ) =>
        new(
            store.Provider,
            seId,
            store.ExternalId,
            name,
            null,
            "perk",
            cost,
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
        );
}
