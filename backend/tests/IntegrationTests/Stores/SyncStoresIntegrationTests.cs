using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class SyncStoresIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _mockSeApi;
    private readonly IEmailNotifier _mockEmailService;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public SyncStoresIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockSeApi = Substitute.For<IStoreProviderClient>();
        _mockEmailService = Substitute.For<IEmailNotifier>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        // The auto-redeem rate floor is switched off here so the cooldown tests below measure the
        // item's own cooldown. The floor has its own tests; leaving it on would make every one of
        // these pass for a reason other than the one it names.
        _factory = new TestWebApplicationFactory(
            _fixture.ConnectionString,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Sync:MinimumAutoRedeemIntervalMinutes"] = "0",
            }
        )
        {
            MockStreamElementsApi = _mockSeApi,
            MockEmailService = _mockEmailService,
        };
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// A tick that finds nothing new must leave the row alone. Measured against a running instance,
    /// the per-item fetch stamp made this table 98% of every row write in the database — 348 updates
    /// a minute against a 348-row table, rewriting its whole contents 1,440 times a day.
    /// <para>
    /// Asserted through the change tracker rather than <c>pg_stat_user_tables</c>: the statistics
    /// collector updates asynchronously, so a counter-based assertion would be flaky. The failure
    /// message names the offending columns.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Quiet_tick_leaves_no_column_dirty()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // One payload instance, applied twice: rebuilding it would stamp a fresh CreatedAt and make
        // the second application a genuine change.
        //
        // CreatedAt is deliberately jagged — 700ns past a whole microsecond, finer than the column
        // can store. DateTimeOffset.UtcNow lands on a whole microsecond about one time in ten, so
        // taking the clock as-is would make this pass by luck nine runs out of ten.
        var jagged = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
        ProviderStoreItem[] payload =
        [
            CreateSeItem("se-1", "ch-1", "Item A", enabled: true, quantity: 5) with
            {
                CreatedAt = jagged,
            },
        ];
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns(payload);

        // Twice. The first tick inserts, and the tick after an insert legitimately writes
        // PreviousAvailability once: the constructor seeds it unavailable, so the next pass settles
        // it to what the item actually is. Steady state — the case that runs 1,439 times a day — is
        // the third pass onwards, and that is what must write nothing.
        await RunSyncAsync();
        await RunSyncAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = scope.ServiceProvider.GetRequiredService<IStoreItemRepository>();

        var tracked = await items.ListByStoreForUpdateAsync(storeId, CancellationToken.None);
        var target = tracked.Single(item => item.ProviderItemId.Value == "se-1");

        // Exactly what the next cron tick applies: the same catalogue, observed a minute later.
        target.UpdateFromApi(payload[0].ToApiData(DateTimeOffset.UtcNow.AddMinutes(1)));

        var dirty = db
            .ChangeTracker.Entries<StoreItem>()
            .Single(entry => entry.Entity.Id == target.Id)
            .Properties.Where(property => property.IsModified)
            .Select(property => property.Metadata.Name)
            .ToArray();

        dirty.Should().BeEmpty();
    }

    /// <summary>
    /// The changed-item count has to fall to zero on a quiet tick, and only a real database can say
    /// so: the second pass compares a payload against values that have been through Postgres and
    /// back, so anything that does not survive storage byte for byte would report every item as
    /// changed, forever.
    /// </summary>
    [Fact]
    public async Task Second_sync_over_an_unchanged_catalogue_reports_nothing_changed()
    {
        await SetupUserWithStoreAsync("ch-1", "Store One");

        // One payload instance, returned by both passes: rebuilding it would stamp a fresh
        // CreatedAt and make the second pass a genuine change.
        ProviderStoreItem[] payload =
        [
            CreateSeItem("se-1", "ch-1", "Item A", enabled: true, quantity: 5),
            CreateSeItem("se-2", "ch-1", "Item B", enabled: false, quantity: 0),
        ];
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns(payload);

        var first = await RunSyncAsync();
        var second = await RunSyncAsync();

        first.ItemsChanged.Should().BeGreaterThan(0);

        second.ItemsProcessed.Should().Be(first.ItemsProcessed);
        second.ItemsChanged.Should().Be(0);
    }

    [Fact]
    public async Task Full_sync_upserts_items_and_updates_LastSyncedAt()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem("se-1", "ch-1", "Item A", enabled: true, quantity: 5),
                CreateSeItem("se-2", "ch-1", "Item B", enabled: false, quantity: 0),
            ]);

        var result = await RunSyncAsync();

        result.StoresSynced.Should().Be(1);
        result.ItemsChanged.Should().BeGreaterOrEqualTo(2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.StoreItems.Where(i => i.WatchedStoreId == storeId).ToListAsync();

        // Init item is updated + se-1 + se-2
        items.Should().Contain(i => i.Name == "Item A");
        items.Should().Contain(i => i.Name == "Item B");

        var store = await db.WatchedStores.FirstAsync(s => s.Id == storeId);
        store.LastSyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Sync_marks_removed_items_unavailable()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // First sync adds a new available item
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("se-new", "ch-1", "New Item", enabled: true, quantity: 5)]);

        await RunSyncAsync();

        // Second sync: item removed from API
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        await RunSyncAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.StoreItems.Where(i => i.WatchedStoreId == storeId).ToListAsync();

        // All items should be unavailable now (not deleted)
        items.Should().AllSatisfy(i => i.Availability.IsAvailable.Should().BeFalse());
    }

    [Fact]
    public async Task Sync_auto_redeems_wishlisted_item()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // Get the init item ID
        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        // Add wishlist entry with auto-redeem
        await AddWishlistEntryAsync(
            accessToken,
            itemId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // Sync: init item becomes available
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItemId,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 5,
                    userInput: ["Email"]
                ),
            ]);

        var result = await RunSyncAsync();

        result.RedemptionsAttempted.Should().Be(1);
        result.RedemptionsPending.Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var redemptions = await db.Redemptions.ToListAsync();
        redemptions.Should().ContainSingle().Which.Status.Should().Be(RedemptionStatus.Pending);

        // SE API should NOT be called for redemption during sync
        await _mockSeApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sync_auto_redeem_creates_pending_when_item_has_no_stock()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        await AddWishlistEntryAsync(
            accessToken,
            itemId,
            autoRedeem: true,
            inputs: ["user@test.com"]
        );

        // Sync: item becomes available (even with limited stock the redemption is created Pending)
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItemId,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 1,
                    userInput: ["Email"]
                ),
            ]);

        var result = await RunSyncAsync();

        result.RedemptionsAttempted.Should().Be(1);
        result.RedemptionsPending.Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var redemptions = await db.Redemptions.ToListAsync();
        redemptions.Should().ContainSingle().Which.Status.Should().Be(RedemptionStatus.Pending);
    }

    /// <summary>
    /// The coverage pass writes wishlist entries in the same cycle that just upserted the store's
    /// items, and both writes reach the same rows through different object graphs. Only a real
    /// DbContext can prove the two do not collide, which is why this lives here rather than beside
    /// the substitute-backed unit tests.
    /// </summary>
    [Fact]
    public async Task Sync_blocks_auto_redeem_entry_when_the_item_gains_an_uncovered_input()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        await AddWishlistEntryAsync(
            accessToken,
            itemId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // The item becomes available but now also asks for a label the store profile has no value
        // for, so the entry loses coverage in the very pass that upserted it.
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItemId,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 5,
                    userInput: ["Email", "Discord Tag"]
                ),
            ]);

        var result = await RunSyncAsync();

        result.StoresSynced.Should().Be(1);
        result.RedemptionsAttempted.Should().Be(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.WishlistEntries.SingleAsync();
        entry.IsBlockedOnInput.Should().BeTrue();
        entry.AutoRedeem.Should().BeFalse();

        var notifications = await db.Notifications.Include(n => n.Items).ToListAsync();
        notifications
            .Should()
            .ContainSingle(n => n.Channel == NotificationChannel.WebUi)
            .Which.Items.Should()
            .ContainSingle(i => i.StoreItemId == itemId);

        (await db.Redemptions.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Sync_sends_notification_for_wishlisted_item()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        // Add wishlist with notify, no auto-redeem
        await AddWishlistEntryAsync(accessToken, itemId, autoRedeem: false, inputs: []);

        // Sync: item becomes available
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem(seItemId, "ch-1", "Init Item", enabled: true, quantity: 3)]);

        var result = await RunSyncAsync();

        result.NotificationsSent.Should().Be(1);

        await _mockEmailService
            .Received(1)
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Is<AvailabilityEmailModel>(m => m.Stores.Count == 1),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = await db.Notifications.Include(n => n.Items).ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].Status.Should().Be(NotificationStatus.Sent);
        notifications[0].Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Sync_handles_new_item_appearing_and_marks_available()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // Sync with a brand new available item (not init-item)
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    "init-item",
                    "ch-1",
                    "Init Item",
                    enabled: false,
                    quantity: 0,
                    userInput: ["Email"]
                ),
                CreateSeItem("se-new", "ch-1", "Brand New Item", enabled: true, quantity: -1),
            ]);

        var result = await RunSyncAsync();

        result.StoresSynced.Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.StoreItems.Where(i => i.WatchedStoreId == storeId).ToListAsync();
        var newItem = items.FirstOrDefault(i => i.Name == "Brand New Item");
        newItem.Should().NotBeNull();
        newItem!.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_skips_inactive_stores()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // Deactivate the store
        var deactivateRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/stores/{storeId}/deactivate"
        );
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        await _client.SendAsync(deactivateRequest);

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("se-1", "ch-1", "Item A", enabled: true, quantity: 5)]);

        var result = await RunSyncAsync();

        result.StoresSynced.Should().Be(0);
        await _mockSeApi
            .DidNotReceive()
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sync_continues_when_one_store_fails()
    {
        // Setup two stores
        var (accessToken, _) = await SetupUserWithStoreAsync("ch-1", "Store One");

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-2"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("init-2", "ch-2", "Init Item 2", enabled: false, quantity: 0)]);
        await AuthenticatedPostAsync(
            accessToken,
            "/api/stores",
            new { channelId = "ch-2", displayName = "Store Two" }
        );

        _mockSeApi.ClearReceivedCalls();

        // ch-1 fails, ch-2 succeeds
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("SE API down"));
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-2"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem("se-2", "ch-2", "Item From Store 2", enabled: true, quantity: 3),
            ]);

        var result = await RunSyncAsync();

        result.StoresSynced.Should().Be(1); // Only store 2 synced
    }

    [Fact]
    public async Task Sync_notification_groups_by_store_with_redemption_status()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // Create a second store
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-2"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("init-2", "ch-2", "Init Item 2", enabled: false, quantity: 0)]);
        await AuthenticatedPostAsync(
            accessToken,
            "/api/stores",
            new { channelId = "ch-2", displayName = "Store Two" }
        );

        // Get item IDs for wishlisting
        var store1Items = await GetStoreItemsAsync(accessToken, storeId);
        var item1Id = store1Items[0].Id;
        var seItem1Id = store1Items[0].ProviderItemId;

        // Get store 2 ID and item
        var listResponse = await AuthenticatedGetAsync(accessToken, "/api/stores");
        var stores = await listResponse.Content.ReadFromJsonAsync<StorePageResponse>();
        var store2 = stores!.Items.First(s => s.DisplayName == "Store Two");
        var store2Items = await GetStoreItemsAsync(accessToken, store2.Id);
        var item2Id = store2Items[0].Id;

        // Wishlist item1 with auto-redeem, item2 with notify only
        await AddWishlistEntryAsync(
            accessToken,
            item1Id,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );
        await AddWishlistEntryAsync(accessToken, item2Id, autoRedeem: false, inputs: []);

        _mockSeApi.ClearReceivedCalls();

        // Both items become available
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItem1Id,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 5,
                    userInput: ["Email"]
                ),
            ]);
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-2"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("init-2", "ch-2", "Init Item 2", enabled: true, quantity: 3)]);

        var result = await RunSyncAsync();

        // Verify email model has both stores
        await _mockEmailService
            .Received(1)
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Is<AvailabilityEmailModel>(m =>
                    m.Stores.Count == 2
                    && m.Stores.Any(s =>
                        s.StoreName == "Store One" && s.Items.Any(i => i.RedemptionPending)
                    )
                    && m.Stores.Any(s =>
                        s.StoreName == "Store Two" && s.Items.Any(i => !i.RedemptionPending)
                    )
                ),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sync_notification_shows_no_redemption_tag_for_notify_only()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        // Wishlist with notify only (no auto-redeem)
        await AddWishlistEntryAsync(accessToken, itemId, autoRedeem: false, inputs: []);

        // Item becomes available
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItemId,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 3,
                    userInput: ["Email"]
                ),
            ]);

        await RunSyncAsync();

        // Verify the email model item has NO redemption pending flag
        await _mockEmailService
            .Received(1)
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Is<AvailabilityEmailModel>(m => !m.Stores[0].Items[0].RedemptionPending),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sync_persists_correct_quantity_after_database_round_trip()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem("se-qty", "ch-1", "Qty Item", enabled: true, quantity: 5)]);

        await RunSyncAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.StoreItems.FirstAsync(i =>
            i.Name == "Qty Item" && i.WatchedStoreId == storeId
        );

        item.Quantity.Total.Should().Be(5);
        item.Quantity.Current.Should().Be(5);
        item.Availability.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_persists_unlimited_quantity_after_database_round_trip()
    {
        var (_, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem("se-unl", "ch-1", "Unlimited Item", enabled: true, quantity: -1),
            ]);

        await RunSyncAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.StoreItems.FirstAsync(i =>
            i.Name == "Unlimited Item" && i.WatchedStoreId == storeId
        );

        item.Quantity.Total.Should().Be(-1);
        item.Quantity.Current.Should().Be(-1);
        item.Availability.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Store_detail_endpoint_returns_correct_quantity_and_availability()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var seItem = CreateSeItem(
            "se-qty73",
            "ch-1",
            "Partial Stock",
            enabled: true,
            quantity: 73
        ) with
        {
            Quantity = new ItemQuantity(110, 73),
        };

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([seItem]);

        await RunSyncAsync();

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var item = storeItems.First(i => i.Name == "Partial Stock");

        item.QuantityTotal.Should().Be(110);
        item.QuantityCurrent.Should().Be(73);
        item.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task State_based_auto_redeem_closes_gap_for_already_available_item()
    {
        var (accessToken, _) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var seItemId = "se-already-available";
        var apiItem = CreateSeItem(
            seItemId,
            "ch-1",
            "Already Available",
            enabled: true,
            quantity: -1,
            userInput: ["Email"]
        );

        // First sync seeds the item (Phase 1 ctor raises transition; not the scenario we want yet,
        // but we need the entity to exist before the wishlist entry can target it).
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([apiItem]);
        await RunSyncAsync();

        Guid itemDbId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await db.StoreItems.FirstAsync(i => i.Name == "Already Available");
            itemDbId = item.Id;
        }

        // Add wishlist entry AFTER item already-available — no transition event will fire on later ticks
        await AddWishlistEntryAsync(
            accessToken,
            itemDbId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // Tick 1 — same SE state, no transition event, but state-based auto-redeem must fire
        var result1 = await RunSyncAsync();
        result1.RedemptionsPending.Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db
                .Redemptions.Where(r => r.StoreItemId == itemDbId)
                .ToListAsync();
            redemptions.Should().ContainSingle().Which.Status.Should().Be(RedemptionStatus.Pending);
            var entry = await db.WishlistEntries.FirstAsync(e => e.StoreItemId == itemDbId);
            entry.LastRedemptionCreatedAt.Should().NotBeNull();
        }

        // Tick 2 — no SE state change, inflight Pending blocks new redemption
        var result2 = await RunSyncAsync();
        result2.RedemptionsPending.Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db
                .Redemptions.Where(r => r.StoreItemId == itemDbId)
                .ToListAsync();
            redemptions.Should().ContainSingle();
        }
    }

    [Fact]
    public async Task Auto_redeem_stops_at_the_desired_count_even_with_no_cooldown()
    {
        var (accessToken, _) = await SetupUserWithStoreAsync("ch-1", "Store One");

        // No cooldown at all — the case that drains a balance a tick at a time. The desired count
        // is the only thing standing between this item and every point the account holds.
        var apiItem = CreateSeItem(
            "se-no-cooldown",
            "ch-1",
            "Unbounded Item",
            enabled: true,
            quantity: -1,
            userInput: ["Email"]
        ) with
        {
            Cooldown = new ItemCooldown(0, 0, 0),
        };

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([apiItem]);
        await RunSyncAsync();

        Guid itemDbId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            itemDbId = (await db.StoreItems.FirstAsync(i => i.Name == "Unbounded Item")).Id;
        }

        await AddWishlistEntryAsync(
            accessToken,
            itemDbId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // Tick A — the one redemption the default count allows.
        (await RunSyncAsync())
            .RedemptionsPending.Should()
            .Be(1);

        // Drive it to a terminal success, so nothing is in flight and only the quota can refuse.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemption = await db.Redemptions.FirstAsync(r => r.StoreItemId == itemDbId);
            redemption.MarkSubmitted("se-id-1", DateTimeOffset.UtcNow);
            redemption.MarkCompleted(DateTimeOffset.UtcNow);
            db.Redemptions.Update(redemption);
            await db.SaveChangesAsync();
        }

        // Ticks B and C — the gate stays shut although the cooldown reopened immediately.
        (await RunSyncAsync())
            .RedemptionsPending.Should()
            .Be(0);
        (await RunSyncAsync()).RedemptionsPending.Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db
                .Redemptions.Where(r => r.StoreItemId == itemDbId)
                .ToListAsync();
            redemptions.Should().ContainSingle();

            // Auto-redeem is still on: the entry is satisfied, not switched off.
            var entry = await db.WishlistEntries.FirstAsync(e => e.StoreItemId == itemDbId);
            entry.AutoRedeem.Should().BeTrue();
        }
    }

    [Fact]
    public async Task State_based_auto_redeem_respects_cooldown_after_completion()
    {
        var (accessToken, _) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var seItemId = "se-cooldown";
        // 60s cooldown
        var apiItem = CreateSeItem(
            seItemId,
            "ch-1",
            "Cooldown Item",
            enabled: true,
            quantity: -1,
            userInput: ["Email"]
        ) with
        {
            Cooldown = new ItemCooldown(60_000, 0, 0),
        };

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([apiItem]);
        await RunSyncAsync();

        Guid itemDbId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            itemDbId = (await db.StoreItems.FirstAsync(i => i.Name == "Cooldown Item")).Id;
        }

        await AddWishlistEntryAsync(
            accessToken,
            itemDbId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // Tick A — produces Pending, complete the redemption manually
        await RunSyncAsync();

        Guid redemptionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemption = await db.Redemptions.FirstAsync(r => r.StoreItemId == itemDbId);
            redemptionId = redemption.Id;
            redemption.MarkSubmitted("se-id-1", DateTimeOffset.UtcNow);
            redemption.MarkCompleted(DateTimeOffset.UtcNow);
            db.Redemptions.Update(redemption);

            // The completed redemption fills the entry's default quota of one. This test is about
            // the cooldown, so the quota is opened up; the quota has its own tests.
            var entry = await db.WishlistEntries.FirstAsync(e => e.StoreItemId == itemDbId);
            entry.SetDesiredRedemptions(null, DateTimeOffset.UtcNow);
            db.WishlistEntries.Update(entry);

            await db.SaveChangesAsync();
        }

        // Tick B — within cooldown, still expect no new Pending
        var resultB = await RunSyncAsync();
        resultB.RedemptionsPending.Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db
                .Redemptions.Where(r => r.StoreItemId == itemDbId)
                .ToListAsync();
            redemptions.Should().ContainSingle();
        }

        // Force cooldown elapsed by rewinding LastRedemptionCreatedAt
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await db.WishlistEntries.FirstAsync(e => e.StoreItemId == itemDbId);
            typeof(WishlistEntry)
                .GetProperty(nameof(WishlistEntry.LastRedemptionCreatedAt))!
                .SetValue(entry, DateTimeOffset.UtcNow.AddSeconds(-120));
            db.WishlistEntries.Update(entry);
            await db.SaveChangesAsync();
        }

        // Tick C — cooldown elapsed; new Pending must be created
        var resultC = await RunSyncAsync();
        resultC.RedemptionsPending.Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db
                .Redemptions.Where(r => r.StoreItemId == itemDbId)
                .ToListAsync();
            redemptions.Should().HaveCount(2);
            redemptions
                .Should()
                .Contain(r => r.Status == RedemptionStatus.Pending && r.Id != redemptionId);

            var entry = await db.WishlistEntries.FirstAsync(e => e.StoreItemId == itemDbId);
            entry.LastRedemptionCreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(-30));
        }
    }

    [Fact]
    public async Task Sync_auto_redeem_skips_when_insufficient_points_then_redeems_when_recovered()
    {
        var (accessToken, storeId) = await SetupUserWithStoreAsync("ch-1", "Store One");

        var storeItems = await GetStoreItemsAsync(accessToken, storeId);
        var itemId = storeItems[0].Id;
        var seItemId = storeItems[0].ProviderItemId;

        await AddWishlistEntryAsync(
            accessToken,
            itemId,
            autoRedeem: true,
            inputs: ["test@test.com"]
        );

        // Item available; Cost = 100 (CreateSeItem default).
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    seItemId,
                    "ch-1",
                    "Init Item",
                    enabled: true,
                    quantity: 5,
                    userInput: ["Email"]
                ),
            ]);

        // Balance below cost → silent skip, no redemption row.
        _mockSeApi
            .GetPointsAsync(
                new StoreExternalId("ch-1"),
                "se-username",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(50L);

        var poor = await RunSyncAsync();
        poor.RedemptionsPending.Should().Be(0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Redemptions.CountAsync()).Should().Be(0);
        }

        // Balance recovers → next tick redeems automatically (re-evaluated each cycle).
        _mockSeApi
            .GetPointsAsync(
                new StoreExternalId("ch-1"),
                "se-username",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1_000L);

        var rich = await RunSyncAsync();
        rich.RedemptionsPending.Should().Be(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var redemptions = await db.Redemptions.ToListAsync();
            redemptions.Should().ContainSingle().Which.Status.Should().Be(RedemptionStatus.Pending);
        }
    }

    #region Helpers

    private sealed record StorePageResponse(
        StoreListResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record StoreListResponse(
        Guid Id,
        string ChannelId,
        string DisplayName,
        bool IsActive
    );

    private async Task<SyncResult> RunSyncAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var syncStores = scope.ServiceProvider.GetRequiredService<SyncStores>();
        return await syncStores.ExecuteAsync(CancellationToken.None);
    }

    private async Task<(string AccessToken, Guid StoreId)> SetupUserWithStoreAsync(
        string channelId,
        string storeName
    )
    {
        var registerPayload = new { email = "sync@test.com", password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "sync@test.com", password = "Password123!" }
        );
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var accessToken = loginBody!.AccessToken;

        // Affordability gate dependencies: stubbed before the credential is saved, because the save
        // checks it and records the username. GetPointsAsync defaults to plenty of points so tests
        // redeem unless they override it.
        _mockSeApi
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("se-username");
        await AuthenticatedPutAsync(
            accessToken,
            "/api/users/me/credentials/streamelements",
            new { secret = "fake-se-token" }
        );

        _mockSeApi
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1_000_000L);

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId(channelId), Arg.Any<CancellationToken>())
            .Returns([
                CreateSeItem(
                    "init-item",
                    channelId,
                    "Init Item",
                    enabled: false,
                    quantity: 0,
                    userInput: ["Email"]
                ),
            ]);

        var createResponse = await AuthenticatedPostAsync(
            accessToken,
            "/api/stores",
            new { channelId, displayName = storeName }
        );

        var storeDetail = await createResponse.Content.ReadFromJsonAsync<StoreDetailResponse>();

        _mockSeApi.ClearReceivedCalls();

        return (accessToken, storeDetail!.Id);
    }

    private async Task<StoreDetailResponse?> GetStoreDetailAsync(string accessToken, Guid storeId)
    {
        var response = await AuthenticatedGetAsync(accessToken, $"/api/stores/{storeId}");
        return await response.Content.ReadFromJsonAsync<StoreDetailResponse>();
    }

    /// <summary>
    /// Items now come from their own endpoint. <c>avail=0</c> turns the default availability filter
    /// off: several of these tests assert on sold-out or disabled items, which the default view hides.
    /// </summary>
    private async Task<StoreItemRowResponse[]> GetStoreItemsAsync(string accessToken, Guid storeId)
    {
        var response = await AuthenticatedGetAsync(
            accessToken,
            $"/api/stores/{storeId}/items?avail=0&limit=200"
        );
        var page = await response.Content.ReadFromJsonAsync<StoreItemPageResponse>();
        return page!.Items;
    }

    private async Task AddWishlistEntryAsync(
        string accessToken,
        Guid storeItemId,
        bool autoRedeem,
        string[] inputs
    )
    {
        // Create entry
        await AuthenticatedPostAsync(accessToken, "/api/wishlist", new { storeItemId });

        // Get entry ID + store ID from the grouped list
        var listResponse = await AuthenticatedGetAsync(accessToken, "/api/wishlist");
        var body = await listResponse.Content.ReadFromJsonAsync<WishlistGroupedListResponse>();
        var storeGroup = body!.Items.First(s => s.Entries.Any(e => e.StoreItemId == storeItemId));
        var entryId = storeGroup.Entries.First(e => e.StoreItemId == storeItemId).Id;
        var storeId = storeGroup.StoreId;

        if (autoRedeem && inputs.Length > 0)
        {
            // Read straight from the database: the item row an endpoint returns carries what a card
            // renders, and the grid never renders input definitions. This is setup, not an
            // assertion, so it takes the shortest correct route rather than widening the contract.
            using var inputScope = _factory.Services.CreateScope();
            var inputDb = inputScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var definitions = await inputDb
                .StoreItems.Where(i => i.Id == storeItemId)
                .Select(i => i.UserInputDefinitions)
                .FirstAsync();

            var profileValues = definitions
                .Take(inputs.Length)
                .Select((label, i) => new { labelCanonical = label, value = inputs[i] })
                .ToArray();
            await AuthenticatedPutAsync(
                accessToken,
                $"/api/stores/{storeId}/profile/values",
                new { values = profileValues }
            );
        }

        // Update with desired settings
        await AuthenticatedPutAsync(
            accessToken,
            $"/api/wishlist/{entryId}",
            new
            {
                notifyOnAvailable = true,
                autoRedeem,
                redemptionMessage = (string?)null,
            }
        );
    }

    private async Task<HttpResponseMessage> AuthenticatedGetAsync(string token, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AuthenticatedPostAsync(
        string token,
        string url,
        object payload
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AuthenticatedPutAsync(
        string token,
        string url,
        object payload
    )
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static ProviderStoreItem CreateSeItem(
        string seId,
        string channelId,
        string name,
        bool enabled,
        int quantity,
        string[]? userInput = null
    )
    {
        return new ProviderStoreItem(
            Provider: StoreProvider.StreamElements,
            Id: new ProviderItemId(seId),
            Store: new StoreExternalId(channelId),
            Name: name,
            Description: null,
            Type: "perk",
            Cost: 100,
            CategoryName: "default",
            Enabled: enabled,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: false,
            UserInputDefinitions: userInput ?? [],
            Sources: [],
            Quantity: new ItemQuantity(quantity, quantity),
            Cooldown: new ItemCooldown(0, 0, 0),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null
        );
    }

    private sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn
    );

    private sealed record StoreDetailResponse(
        Guid Id,
        string ChannelId,
        string DisplayName,
        bool IsActive,
        DateTimeOffset? LastSyncedAt,
        DateTimeOffset CreatedAt,
        int ItemCount
    );

    private sealed record StoreItemPageResponse(
        StoreItemRowResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record StoreItemRowResponse(
        Guid Id,
        string ProviderItemId,
        string Name,
        string? Description,
        long Cost,
        string CategoryName,
        bool SubscriberOnly,
        bool IsAvailable,
        int QuantityCurrent,
        int QuantityTotal,
        bool AllowMessages,
        string? ThumbnailUrl,
        Guid? WishlistEntryId,
        DateTimeOffset ProviderCreatedAt
    );

    private sealed record WishlistGroupedListResponse(
        WishlistStoreGroupResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record WishlistStoreGroupResponse(
        Guid StoreId,
        string StoreName,
        bool IsActive,
        DateTimeOffset? LastSyncedAt,
        WishlistEntryListItemResponse[] Entries
    );

    private sealed record WishlistEntryListItemResponse(
        Guid Id,
        Guid StoreItemId,
        string ItemName,
        long ItemCost,
        bool IsAvailable,
        bool NotifyOnAvailable,
        bool AutoRedeem,
        bool HasStaleInputs,
        DateTimeOffset CreatedAt
    );

    #endregion
}
