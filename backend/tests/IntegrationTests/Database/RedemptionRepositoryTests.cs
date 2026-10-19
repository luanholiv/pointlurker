using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Database;

[Collection(ContainersCollection.Name)]
public sealed class RedemptionRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private RedemptionRepository _sut = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        _db = new AppDbContext(options);
        _sut = new RedemptionRepository(_db);
        _unitOfWork = new UnitOfWork(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetPendingForUpdateAsync_returns_pending_with_null_next_retry()
    {
        var redemption = await AddPendingRedemption();

        var result = await _sut.GetPendingForUpdateAsync(DateTimeOffset.UtcNow);

        result.Should().ContainSingle(r => r.Id == redemption.Id);
    }

    [Fact]
    public async Task GetPendingForUpdateAsync_returns_pending_with_past_next_retry()
    {
        var redemption = await AddPendingRedemption();
        redemption.ScheduleRetry(DateTimeOffset.UtcNow.AddMinutes(-5));
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.GetPendingForUpdateAsync(DateTimeOffset.UtcNow);

        result.Should().ContainSingle(r => r.Id == redemption.Id);
    }

    [Fact]
    public async Task GetPendingForUpdateAsync_excludes_pending_with_future_next_retry()
    {
        var redemption = await AddPendingRedemption();
        redemption.ScheduleRetry(DateTimeOffset.UtcNow.AddMinutes(5));
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.GetPendingForUpdateAsync(DateTimeOffset.UtcNow);

        result.Should().NotContain(r => r.Id == redemption.Id);
    }

    [Fact]
    public async Task GetPendingForUpdateAsync_excludes_non_pending_statuses()
    {
        var redemption = await AddPendingRedemption();
        redemption.MarkSubmitted("se_1", DateTimeOffset.UtcNow);
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.GetPendingForUpdateAsync(DateTimeOffset.UtcNow);

        result.Should().NotContain(r => r.Id == redemption.Id);
    }

    [Fact]
    public async Task GetSubmittedForUpdateAsync_returns_only_submitted()
    {
        var submitted = await AddSubmittedRedemption();
        var pending = await AddPendingRedemption();

        var result = await _sut.GetSubmittedForUpdateAsync();

        result.Should().ContainSingle(r => r.Id == submitted.Id);
        result.Should().NotContain(r => r.Id == pending.Id);
    }

    [Fact]
    public async Task GetSubmittedForUpdateAsync_ordered_by_submitted_at_ascending()
    {
        var r1 = await AddSubmittedRedemption(DateTimeOffset.UtcNow.AddSeconds(-10));
        var r2 = await AddSubmittedRedemption(DateTimeOffset.UtcNow.AddSeconds(-5));

        var result = await _sut.GetSubmittedForUpdateAsync();

        result.Select(r => r.Id).Should().ContainInOrder(r1.Id, r2.Id);
    }

    [Fact]
    public async Task ListAsync_filter_by_status()
    {
        var pending = await AddPendingRedemption();
        var submitted = await AddSubmittedRedemption();

        var (items, total) = await _sut.ListAsync(
            new RedemptionFilter(RedemptionStatus.Pending, null, null, null),
            offset: 0,
            limit: 20
        );

        total.Should().Be(1);
        items.Should().ContainSingle(r => r.Id == pending.Id);
    }

    [Fact]
    public async Task ListAsync_filter_by_wishlist_entry()
    {
        var r1 = await AddPendingRedemption();
        var r2 = await AddPendingRedemption(); // different entry

        var (items, total) = await _sut.ListAsync(
            new RedemptionFilter(null, r1.WishlistEntryId, null, null),
            offset: 0,
            limit: 20
        );

        total.Should().Be(1);
        items.Should().ContainSingle(r => r.Id == r1.Id);
    }

    [Fact]
    public async Task ListAsync_filter_by_date_range()
    {
        var r1 = await AddPendingRedemption();

        var from = DateTimeOffset.UtcNow.AddSeconds(-1);
        var to = DateTimeOffset.UtcNow.AddSeconds(1);

        var (items, total) = await _sut.ListAsync(
            new RedemptionFilter(null, null, from, to),
            offset: 0,
            limit: 20
        );

        total.Should().BeGreaterThan(0);
        items.Should().Contain(r => r.Id == r1.Id);
    }

    [Fact]
    public async Task ListAsync_pagination()
    {
        for (var i = 0; i < 5; i++)
            await AddPendingRedemption();

        var (page1, total) = await _sut.ListAsync(
            new RedemptionFilter(null, null, null, null),
            offset: 0,
            limit: 2
        );
        var (page2, _) = await _sut.ListAsync(
            new RedemptionFilter(null, null, null, null),
            offset: 2,
            limit: 2
        );

        total.Should().BeGreaterOrEqualTo(5);
        page1.Count.Should().Be(2);
        page1.Should().NotIntersectWith(page2, "pages must be distinct");
    }

    [Fact]
    public async Task Snapshot_columns_persisted_and_read_back_correctly()
    {
        var (store, item) = await SeedRequiredEntities();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            item.Id,
            DateTimeOffset.UtcNow
        );
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var redemption = entry.CreateRedemption(
            item,
            null,
            "VIP Badge",
            "xQc",
            DateTimeOffset.UtcNow
        );
        _sut.Add(redemption);
        await _unitOfWork.SaveChangesAsync();

        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemption.Id);
        reloaded.StoreItemName.Should().Be("VIP Badge");
        reloaded.StoreName.Should().Be("xQc");
    }

    [Fact]
    public async Task Backfill_sql_sets_correct_names_for_existing_rows()
    {
        var (store, item) = await SeedRequiredEntities();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            item.Id,
            DateTimeOffset.UtcNow
        );
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        // Insert redemption with blank snapshot values (simulates pre-migration row)
        var redemptionId = Guid.NewGuid();
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO redemptions ("Id","WishlistEntryId","StoreItemId","StoreItemName","StoreName","StoreExternalId","ProviderItemId","Status","Attempts","AttemptedAt")
            VALUES ({redemptionId},{entry.Id},{item.Id},'','','ch-test','se-test','Pending',0,NOW())
            """
        );

        // Run the backfill SQL from the migration
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE redemptions r
            SET "StoreItemName" = COALESCE(si."Name", ''),
                "StoreName"     = COALESCE(ws."DisplayName", '')
            FROM store_items si
            LEFT JOIN watched_stores ws ON ws."Id" = si."WatchedStoreId"
            WHERE si."Id" = r."StoreItemId"
            AND (r."StoreItemName" = '' OR r."StoreName" = '')
            """
        );

        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemptionId);
        reloaded.StoreItemName.Should().Be(item.Name);
        reloaded.StoreName.Should().Be(store.DisplayName);
    }

    [Fact]
    public async Task Backfill_sql_leaves_empty_string_for_orphaned_store_item_id()
    {
        // Seed a valid WishlistEntry so the FK on redemptions.WishlistEntryId is satisfied
        var (_, realItem) = await SeedRequiredEntities();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            realItem.Id,
            DateTimeOffset.UtcNow
        );
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        // Insert a redemption with a StoreItemId that does NOT exist in store_items
        var orphanedStoreItemId = Guid.NewGuid();
        var redemptionId = Guid.NewGuid();
        await _db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO redemptions ("Id","WishlistEntryId","StoreItemId","StoreItemName","StoreName","StoreExternalId","ProviderItemId","Status","Attempts","AttemptedAt")
            VALUES ({redemptionId},{entry.Id},{orphanedStoreItemId},'','','ch-test','se-orphan','Pending',0,NOW())
            """
        );

        // Run the backfill SQL
        await _db.Database.ExecuteSqlRawAsync(
            """
            UPDATE redemptions r
            SET "StoreItemName" = COALESCE(si."Name", ''),
                "StoreName"     = COALESCE(ws."DisplayName", '')
            FROM store_items si
            LEFT JOIN watched_stores ws ON ws."Id" = si."WatchedStoreId"
            WHERE si."Id" = r."StoreItemId"
            AND (r."StoreItemName" = '' OR r."StoreName" = '')
            """
        );

        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemptionId);
        reloaded.StoreItemName.Should().Be("");
        reloaded.StoreName.Should().Be("");
    }

    [Fact]
    public async Task ListAsync_ordered_by_attempted_at_descending()
    {
        var r1 = await AddPendingRedemption();
        var r2 = await AddPendingRedemption();

        var earlier = DateTimeOffset.UtcNow.AddSeconds(-10);
        var later = DateTimeOffset.UtcNow;
        await _db.Database.ExecuteSqlAsync(
            $"UPDATE redemptions SET \"AttemptedAt\" = {earlier} WHERE \"Id\" = {r1.Id}"
        );
        await _db.Database.ExecuteSqlAsync(
            $"UPDATE redemptions SET \"AttemptedAt\" = {later} WHERE \"Id\" = {r2.Id}"
        );

        var (items, _) = await _sut.ListAsync(
            new RedemptionFilter(null, null, null, null),
            offset: 0,
            limit: 20
        );

        items.Select(r => r.Id).Should().ContainInOrder(r2.Id, r1.Id);
    }

    [Fact]
    public async Task ListByWishlistEntryAsync_returns_redemptions_for_entry()
    {
        var redemption = await AddPendingRedemption();

        var result = await _sut.ListByWishlistEntryAsync(redemption.WishlistEntryId);

        result.Should().ContainSingle(r => r.Id == redemption.Id);
    }

    [Fact]
    public async Task ListByWishlistEntryAsync_returns_empty_for_unknown_entry()
    {
        var result = await _sut.ListByWishlistEntryAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListByWishlistEntryAsync_returns_only_entries_for_given_wishlist_entry()
    {
        var r1 = await AddPendingRedemption();
        var r2 = await AddPendingRedemption();

        var result = await _sut.ListByWishlistEntryAsync(r1.WishlistEntryId);

        result.Should().ContainSingle(r => r.Id == r1.Id);
        result.Should().NotContain(r => r.Id == r2.Id);
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_omits_an_entry_with_no_redemptions()
    {
        var entryId = Guid.NewGuid();

        var result = await _sut.CountHoldingQuotaAsync([entryId]);

        result.Should().NotContainKey(entryId);
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_answers_nothing_when_asked_about_nothing()
    {
        var result = await _sut.CountHoldingQuotaAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_counts_a_pending_redemption_as_held_and_inflight()
    {
        var redemption = await AddPendingRedemption();

        var result = await _sut.CountHoldingQuotaAsync([redemption.WishlistEntryId]);

        result[redemption.WishlistEntryId]
            .Should()
            .Be(new RedemptionQuotaUsage(QuotaUsed: 1, Inflight: 1));
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_counts_a_submitted_redemption_as_held_and_inflight()
    {
        var redemption = await AddSubmittedRedemption();

        var result = await _sut.CountHoldingQuotaAsync([redemption.WishlistEntryId]);

        result[redemption.WishlistEntryId]
            .Should()
            .Be(new RedemptionQuotaUsage(QuotaUsed: 1, Inflight: 1));
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_keeps_a_completed_redemption_held_but_not_inflight()
    {
        var redemption = await AddSubmittedRedemption();
        redemption.MarkCompleted(DateTimeOffset.UtcNow);
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.CountHoldingQuotaAsync([redemption.WishlistEntryId]);

        result[redemption.WishlistEntryId]
            .Should()
            .Be(new RedemptionQuotaUsage(QuotaUsed: 1, Inflight: 0));
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_releases_the_slot_of_a_failed_submission()
    {
        var redemption = await AddPendingRedemption();
        redemption.MarkFailed(SubmissionFailureCode.Unknown);
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.CountHoldingQuotaAsync([redemption.WishlistEntryId]);

        result.Should().NotContainKey(redemption.WishlistEntryId);
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_accumulates_across_an_entrys_redemptions()
    {
        var (store, storeItem) = await SeedRequiredEntities();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            storeItem.Id,
            DateTimeOffset.UtcNow
        );
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        foreach (var _ in Enumerable.Range(0, 3))
        {
            _sut.Add(
                entry.CreateRedemption(
                    storeItem,
                    null,
                    storeItem.Name,
                    store.DisplayName,
                    DateTimeOffset.UtcNow
                )
            );
        }
        await _unitOfWork.SaveChangesAsync();

        var result = await _sut.CountHoldingQuotaAsync([entry.Id]);

        result[entry.Id].QuotaUsed.Should().Be(3);
    }

    [Fact]
    public async Task CountHoldingQuotaAsync_scopes_each_count_to_its_own_entry()
    {
        var r1 = await AddPendingRedemption();
        var r2 = await AddPendingRedemption(); // different entry

        var result = await _sut.CountHoldingQuotaAsync([r1.WishlistEntryId, r2.WishlistEntryId]);

        result[r1.WishlistEntryId].QuotaUsed.Should().Be(1);
        result[r2.WishlistEntryId].QuotaUsed.Should().Be(1);
    }

    [Fact]
    public async Task Existing_rows_default_to_zero_attempts()
    {
        var redemption = await AddPendingRedemption();

        var reloaded = await _db.Redemptions.SingleAsync(r => r.Id == redemption.Id);
        reloaded.Attempts.Should().Be(0);
    }

    private async Task<Redemption> AddPendingRedemption()
    {
        var (store, storeItem) = await SeedRequiredEntities();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            storeItem.Id,
            DateTimeOffset.UtcNow
        );
        _db.WishlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var redemption = entry.CreateRedemption(
            storeItem,
            null,
            storeItem.Name,
            store.DisplayName,
            DateTimeOffset.UtcNow
        );
        _sut.Add(redemption);
        await _unitOfWork.SaveChangesAsync();
        return redemption;
    }

    private async Task<Redemption> AddSubmittedRedemption(DateTimeOffset? submittedAt = null)
    {
        var redemption = await AddPendingRedemption();
        redemption.MarkSubmitted(
            "se_" + Guid.NewGuid().ToString("N"),
            submittedAt ?? DateTimeOffset.UtcNow
        );
        _sut.Update(redemption);
        await _unitOfWork.SaveChangesAsync();
        return redemption;
    }

    private async Task<(WatchedStore Store, StoreItem Item)> SeedRequiredEntities()
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-test"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        _db.WatchedStores.Add(store);

        var item = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-" + Guid.NewGuid().ToString("N")),
                new StoreExternalId("ch-test"),
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
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow
            )
        );
        item.ClearDomainEvents();
        _db.StoreItems.Add(item);
        await _db.SaveChangesAsync();

        return (store, item);
    }
}
