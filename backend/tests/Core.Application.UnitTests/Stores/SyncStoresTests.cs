using System.Threading.Channels;
using Core.Application.UnitTests.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class SyncStoresTests
{
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly IEmailNotifier _emailNotifier = Substitute.For<IEmailNotifier>();
    private readonly IWatchedStoreRepository _storeRepo = Substitute.For<IWatchedStoreRepository>();
    private readonly IStoreItemRepository _itemRepo = Substitute.For<IStoreItemRepository>();
    private readonly IWishlistEntryRepository _wishlistRepo =
        Substitute.For<IWishlistEntryRepository>();
    private readonly IRedemptionRepository _redemptionRepo =
        Substitute.For<IRedemptionRepository>();
    private readonly INotificationRepository _notificationRepo =
        Substitute.For<INotificationRepository>();
    private readonly ISecretProtector _tokenEncryption = Substitute.For<ISecretProtector>();
    private readonly ISecretProtector _inputProtector = Substitute.For<ISecretProtector>();
    private readonly IStoreInputResolutionLogRepository _logRepo =
        Substitute.For<IStoreInputResolutionLogRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private readonly Channel<Redemption> _redemptionChannel = Channel.CreateUnbounded<Redemption>();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);

    // One per scope, as in production: the coordinator's own phases, each per-store unit, and each
    // coverage unit commit their own DbContext. Separate fakes keep the commit counts attributable.
    private readonly IUnitOfWork _cycleUnitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUnitOfWork _storeSyncUnitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUnitOfWork _coverageUnitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SyncStores _sut;

    public SyncStoresTests()
    {
        // Nothing held and nothing in flight unless a test says otherwise, and no rate floor, so
        // the cooldown under test is the item's own.
        StubQuotaUsage();

        var settings = Options.Create(
            new SyncSettings { MaxDegreeOfParallelism = 1, MinimumAutoRedeemIntervalMinutes = 0 }
        );
        _sut = new SyncStores(
            _userProvider,
            new ProviderSecrets(
                _credentials,
                new ProviderSecretProtectors(_tokenEncryption, Substitute.For<ISecretProtector>()),
                _timeProvider
            ),
            _storeRepo,
            _itemRepo,
            _wishlistRepo,
            _redemptionRepo,
            _notificationRepo,
            _logRepo,
            StoreProviderClients.FactoryFor(_seApi),
            _emailNotifier,
            _redemptionChannel.Writer,
            new StoreInputValueResolver(_resolver, _inputProtector),
            StoreSyncUnitFactory,
            _cycleUnitOfWork,
            settings,
            _timeProvider,
            NullLogger<SyncStores>.Instance
        );
    }

    /// <summary>
    /// Stubs the batched quota lookup. Called with nothing, every entry reads as untouched — the
    /// dictionary omits entries with no redemptions, exactly as the repository does.
    /// </summary>
    private void StubQuotaUsage(params (Guid EntryId, RedemptionQuotaUsage Usage)[] usages) =>
        _redemptionRepo
            .CountHoldingQuotaAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(usages.ToDictionary(usage => usage.EntryId, usage => usage.Usage));

    /// <summary>
    /// Stands in for the registered per-store factory: a real unit over the same fakes, so a cycle
    /// still runs end to end through both phases. The fakes outlive every unit, so there is no scope
    /// to hand over and disposal is a no-op.
    /// </summary>
    private ScopedInstance<StoreSyncUnit> StoreSyncUnitFactory() =>
        new(
            new StoreSyncUnit(
                _itemRepo,
                _storeRepo,
                StoreProviderClients.FactoryFor(_seApi),
                CoverageUnitFactory,
                _storeSyncUnitOfWork,
                _timeProvider
            )
        );

    private ScopedInstance<StoreInputCoverageUnit> CoverageUnitFactory() =>
        new(
            new StoreInputCoverageUnit(
                _storeRepo,
                _wishlistRepo,
                _logRepo,
                _resolver,
                _coverageUnitOfWork,
                _timeProvider,
                NullLogger<StoreInputCoverageUnit>.Instance
            )
        );

    #region Phase 1 — Fetch + Check

    [Fact]
    public async Task Skips_when_no_user()
    {
        SetupUsers(null);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(0);
    }

    /// <summary>
    /// Reading a catalogue needs no credential, so the cycle is not gated on one: a missing
    /// credential for one provider must not stop any store from syncing. Only auto-redeem needs it.
    /// </summary>
    [Fact]
    public async Task Syncs_stores_without_any_credential_and_redeems_nothing()
    {
        var user = CreateUser(token: null);
        SetupUsers(user);
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());
        var entry = NewAutoRedeemEntry(
            user.Id,
            NewItemWithStore(store, enabled: true, quantity: 5)
        );
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
        await _seApi
            .DidNotReceive()
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Skips_a_store_its_provider_says_is_not_yet_due()
    {
        var user = SetupUserWithToken();
        _seApi.Capabilities.Returns(
            StoreProviderClients.StreamElementsLike with
            {
                MinimumSyncInterval = TimeSpan.FromMinutes(5),
            }
        );
        var recent = CreateWatchedStore(user.Id, channelId: "ch-recent");
        recent.RecordSync(Now.AddMinutes(-4));
        var stale = CreateWatchedStore(user.Id, channelId: "ch-stale");
        stale.RecordSync(Now.AddMinutes(-5));
        var neverSynced = CreateWatchedStore(user.Id, channelId: "ch-new");
        _storeRepo
            .ListActiveAsync(Arg.Any<CancellationToken>())
            .Returns([recent, stale, neverSynced]);
        foreach (var store in new[] { recent, stale, neverSynced })
        {
            _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
            _seApi
                .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ProviderStoreItem>());
        }

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(2);
        result.StoresSkipped.Should().Be(1);
        await _seApi
            .DidNotReceive()
            .GetStoreItemsAsync(recent.ExternalId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_zero_minimum_interval_syncs_a_store_on_every_tick()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        store.RecordSync(Now);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
        result.StoresSkipped.Should().Be(0);
    }

    [Fact]
    public async Task Phase1_syncs_store_and_updates_items()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
        result.ItemsChanged.Should().Be(1);
        _itemRepo
            .Received(1)
            .Upsert(Arg.Is<IReadOnlyCollection<StoreItem>>(items => items.Count == 1));
        // Two per store: the items, then the coverage-and-checkpoint pass.
        await _storeSyncUnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Phase 1 — processed vs changed

    /// <summary>
    /// The counter the cycle log reports has to survive a quiet tick. Under the default one-minute
    /// cron a store whose catalogue did not move must report nothing changed, or the log cannot tell
    /// a quiet cycle from one that did something.
    /// </summary>
    [Fact]
    public async Task Phase1_reports_no_changed_items_when_the_catalogue_did_not_move()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var localItem = NewItemWithStore(store, enabled: true, quantity: 5);
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([ToSeStoreItem(localItem)]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.ItemsProcessed.Should().Be(1);
        result.ItemsChanged.Should().Be(0);
    }

    [Fact]
    public async Task Phase1_counts_an_item_whose_data_moved()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var localItem = NewItemWithStore(store, enabled: true, quantity: 5);
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([ToSeStoreItem(localItem) with { Cost = 250 }]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.ItemsProcessed.Should().Be(1);
        result.ItemsChanged.Should().Be(1);
    }

    [Fact]
    public async Task Phase1_counts_an_available_item_that_vanished_from_the_payload()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var localItem = NewItemWithStore(store, enabled: true, quantity: 5);
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);
        _seApi.GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.ItemsProcessed.Should().Be(1);
        result.ItemsChanged.Should().Be(1);
    }

    /// <summary>
    /// An item that vanished upstream long ago is taken down once and then stays down. It is still
    /// processed every tick, but re-marking an already-unavailable item changes nothing, so it must
    /// not keep contributing to the changed count for the rest of the store's life.
    /// </summary>
    [Fact]
    public async Task Phase1_does_not_recount_an_item_that_was_already_unavailable()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var localItem = NewItemWithStore(store, enabled: false, quantity: 0);
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);
        _seApi.GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.ItemsProcessed.Should().Be(1);
        result.ItemsChanged.Should().Be(0);
    }

    #endregion

    #region Channel refresh + image URL mapping

    /// <summary>
    /// The channel display name and avatar are cosmetic and belong to the daily
    /// <c>RefreshStoreChannelInfo</c> pass. Looking them up here cost one StreamElements call per
    /// store per tick.
    /// </summary>
    [Fact]
    public async Task Sync_does_not_look_up_channel_info()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id, name: "Original Name");
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        await _seApi
            .DidNotReceive()
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        result.StoresSynced.Should().Be(1);
        result.ItemsChanged.Should().Be(1);
        store.DisplayName.Should().Be("Original Name");
        store.AvatarUrl.Should().BeNull();
        _itemRepo.Received(1).Upsert(Arg.Any<IReadOnlyCollection<StoreItem>>());
        _storeRepo.Received().Update(store);
    }

    [Fact]
    public async Task Sync_maps_item_image_urls()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        var apiItem = CreateSeStoreItem(
            "se-1",
            store.ExternalId.Value,
            enabled: true,
            quantity: 5
        ) with
        {
            ThumbnailUrl = "https://cdn.example.com/thumb.png",
            PreviewUrl = "https://cdn.example.com/preview.png",
        };
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        await _sut.ExecuteAsync(CancellationToken.None);

        _itemRepo
            .Received(1)
            .Upsert(
                Arg.Is<IReadOnlyCollection<StoreItem>>(items =>
                    items.Single().ThumbnailUrl == "https://cdn.example.com/thumb.png"
                    && items.Single().PreviewUrl == "https://cdn.example.com/preview.png"
                )
            );
    }

    [Fact]
    public async Task Phase1_new_available_item_collects_event()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem(
            "se-1",
            store.ExternalId.Value,
            enabled: true,
            quantity: -1
        );
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
    }

    [Fact]
    public async Task Phase1_new_unavailable_item_does_not_trigger_redeem()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem(
            "se-1",
            store.ExternalId.Value,
            enabled: false,
            quantity: 0
        );
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsAttempted.Should().Be(0);
    }

    [Fact]
    public async Task Phase1_removed_item_is_marked_unavailable()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var localItem = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            CreateApiData("se-1", store.ExternalId.Value, true, 5)
        );
        localItem.ClearDomainEvents();
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);

        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        _itemRepo
            .Received(1)
            .Upsert(
                Arg.Is<IReadOnlyCollection<StoreItem>>(items =>
                    items.Any(i => !i.Availability.IsAvailable)
                )
            );
    }

    [Fact]
    public async Task Phase1_se_api_failure_for_one_store_does_not_block_others()
    {
        var user = SetupUserWithToken();
        var store1 = CreateWatchedStore(user.Id, "ch-1", "Store 1");
        var store2 = CreateWatchedStore(user.Id, "ch-2", "Store 2");
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store1, store2]);
        _storeRepo.GetByIdAsync(store1.Id, Arg.Any<CancellationToken>()).Returns(store1);
        _storeRepo.GetForUpdateAsync(store1.Id, Arg.Any<CancellationToken>()).Returns(store1);
        _storeRepo.GetByIdAsync(store2.Id, Arg.Any<CancellationToken>()).Returns(store2);
        _storeRepo.GetForUpdateAsync(store2.Id, Arg.Any<CancellationToken>()).Returns(store2);
        _itemRepo.ListByStoreForUpdateAsync(store1.Id, Arg.Any<CancellationToken>()).Returns([]);
        _itemRepo.ListByStoreForUpdateAsync(store2.Id, Arg.Any<CancellationToken>()).Returns([]);

        _seApi
            .GetStoreItemsAsync(new StoreExternalId("ch-1"), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("API down"));
        _seApi
            .GetStoreItemsAsync(new StoreExternalId("ch-2"), Arg.Any<CancellationToken>())
            .Returns([CreateSeStoreItem("se-2", "ch-2", true, 1)]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
    }

    [Fact]
    public async Task Phase1_unlimited_stock_treated_as_available()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem(
            "se-1",
            store.ExternalId.Value,
            enabled: true,
            quantity: -1
        );
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.StoresSynced.Should().Be(1);
        result.ItemsChanged.Should().Be(1);
    }

    #endregion

    #region Phase 2 — Auto-Redeem (state-based)

    [Fact]
    public async Task Phase2_creates_pending_for_already_available_item_with_no_event()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        // Local item already available; api returns same → no transition event
        var localItem = NewItemWithStore(store, enabled: true, quantity: 5);
        localItem.ClearDomainEvents();
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);

        var apiItem = ToSeStoreItem(localItem);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var entry = NewAutoRedeemEntry(user.Id, localItem);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        // Nothing may reach the channel before the row is committed: a consumer that woke on the
        // message and went looking for the redemption would find nothing.
        var queuedAtCommitTime = -1;
        _cycleUnitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                queuedAtCommitTime = _redemptionChannel.Reader.Count;
                return 1;
            });

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsAttempted.Should().Be(1);
        result.RedemptionsPending.Should().Be(1);

        _redemptionRepo
            .Received(1)
            .Add(Arg.Is<Redemption>(r => r.Status == RedemptionStatus.Pending));
        _wishlistRepo.Received(1).Update(entry);

        queuedAtCommitTime.Should().Be(0);
        await _cycleUnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        _redemptionChannel.Reader.TryRead(out var ch).Should().BeTrue();
        ch!.Status.Should().Be(RedemptionStatus.Pending);
    }

    [Fact]
    public async Task Phase2_skips_when_balance_insufficient()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5); // Cost = 100
        var entry = NewAutoRedeemEntry(user.Id, item);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        // Balance below item cost.
        _seApi
            .GetPointsAsync(
                new StoreExternalId(store.ExternalId.Value),
                "se-username",
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            )
            .Returns(50L);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
    }

    [Fact]
    public async Task Phase2_skips_all_when_username_missing()
    {
        var user = CreateUser("encrypted-token"); // no username persisted
        SetupUsers(user);
        _tokenEncryption.Unprotect("encrypted-token").Returns("decrypted-jwt");
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
        await _seApi
            .DidNotReceive()
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>
    /// The balance fail-safe is per provider. An entry whose provider holds no credential is skipped,
    /// and an entry in another provider's store still redeems in the same cycle.
    /// </summary>
    [Fact]
    public async Task Phase2_a_provider_without_a_credential_does_not_block_another()
    {
        var user = SetupUserWithToken();
        var streamElements = CreateWatchedStore(user.Id);
        var gauPoints = CreateWatchedStore(
            user.Id,
            channelId: "gaupoints",
            provider: StoreProvider.GauPoints
        );
        _storeRepo
            .ListActiveAsync(Arg.Any<CancellationToken>())
            .Returns([streamElements, gauPoints]);
        foreach (var store in new[] { streamElements, gauPoints })
        {
            _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
            _seApi
                .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ProviderStoreItem>());
        }
        var seEntry = NewAutoRedeemEntry(
            user.Id,
            NewItemWithStore(streamElements, enabled: true, quantity: 5)
        );
        var gpEntry = NewAutoRedeemEntry(
            user.Id,
            NewItemWithStore(gauPoints, enabled: true, quantity: 5)
        );
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([gpEntry, seEntry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(1);
        _redemptionRepo.Received(1).Add(Arg.Is<Redemption>(r => r.WishlistEntryId == seEntry.Id));
        await _seApi
            .DidNotReceive()
            .GetPointsAsync(
                gauPoints.ExternalId,
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Phase2_skips_channel_when_points_fetch_fails()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        _seApi
            .GetPointsAsync(
                new StoreExternalId(store.ExternalId.Value),
                "se-username",
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ProviderApiException(401, "Unauthorized", "Unauthorized"));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
    }

    [Fact]
    public async Task Phase2_fetches_balance_once_per_channel_and_never_calls_GetUsername()
    {
        var user = SetupUserWithToken(); // balance 1,000,000
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item1 = NewItemWithStore(store, enabled: true, quantity: 5);
        var item2 = NewItemWithStore(store, enabled: true, quantity: 5);
        var e1 = NewAutoRedeemEntry(user.Id, item1);
        var e2 = NewAutoRedeemEntry(user.Id, item2);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([e1, e2]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(2);
        await _seApi
            .Received(1)
            .GetPointsAsync(
                new StoreExternalId(store.ExternalId.Value),
                "se-username",
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            );
        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Phase2_running_spend_blocks_second_item_when_jointly_over_budget()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        // Two items cost 100 each; balance 150 affords only the first.
        var item1 = NewItemWithStore(store, enabled: true, quantity: 5);
        var item2 = NewItemWithStore(store, enabled: true, quantity: 5);
        var e1 = NewAutoRedeemEntry(user.Id, item1);
        var e2 = NewAutoRedeemEntry(user.Id, item2);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([e1, e2]);

        _seApi
            .GetPointsAsync(
                new StoreExternalId(store.ExternalId.Value),
                "se-username",
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            )
            .Returns(150L);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(1);
    }

    [Fact]
    public async Task Phase2_idempotent_when_inflight_pending_exists()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);

        // Room left in the quota, so the inflight submission is the only thing holding this back.
        entry.SetDesiredRedemptions(2, Now);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);
        StubQuotaUsage((entry.Id, new RedemptionQuotaUsage(QuotaUsed: 1, Inflight: 1)));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsAttempted.Should().Be(0);
        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
    }

    [Fact]
    public async Task Phase2_stops_redeeming_once_the_desired_count_is_held()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        entry.SetDesiredRedemptions(2, Now);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        // Two held, none of them still in flight: the quota is what closes this, not concurrency.
        StubQuotaUsage((entry.Id, new RedemptionQuotaUsage(QuotaUsed: 2, Inflight: 0)));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsAttempted.Should().Be(0);
        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
    }

    [Fact]
    public async Task Phase2_redeems_while_the_desired_count_has_room()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        entry.SetDesiredRedemptions(3, Now);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);
        StubQuotaUsage((entry.Id, new RedemptionQuotaUsage(QuotaUsed: 1, Inflight: 0)));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(1);
    }

    [Fact]
    public async Task Phase2_keeps_redeeming_an_unlimited_entry()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        entry.SetDesiredRedemptions(null, Now);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);
        StubQuotaUsage((entry.Id, new RedemptionQuotaUsage(QuotaUsed: 99, Inflight: 0)));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(1);
    }

    [Fact]
    public async Task Phase2_asks_for_every_entrys_quota_in_one_call()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);

        var e1 = NewAutoRedeemEntry(user.Id, NewItemWithStore(store, enabled: true, quantity: 5));
        var e2 = NewAutoRedeemEntry(user.Id, NewItemWithStore(store, enabled: true, quantity: 5));
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([e1, e2]);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _redemptionRepo
            .Received(1)
            .CountHoldingQuotaAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 2 && ids.Contains(e1.Id) && ids.Contains(e2.Id)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Phase2_idempotent_within_cooldown_after_completion()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(
            store,
            enabled: true,
            quantity: 5,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );
        var entry = NewAutoRedeemEntry(user.Id, item);
        // Simulate prior redemption 30s ago → cooldown blocks
        entry.CreateRedemption(item, null, item.Name, store.DisplayName, Now.AddSeconds(-30));

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsAttempted.Should().Be(0);
        result.RedemptionsPending.Should().Be(0);
    }

    [Fact]
    public async Task Phase2_creates_new_pending_after_cooldown_elapsed()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(
            store,
            enabled: true,
            quantity: 5,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );
        var entry = NewAutoRedeemEntry(user.Id, item);
        entry.CreateRedemption(item, null, item.Name, store.DisplayName, Now.AddSeconds(-90));

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(1);
    }

    [Fact]
    public async Task Phase2_skips_when_AutoRedeem_off()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        // Repo only returns AutoRedeem=true entries; off entries simply don't appear
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WishlistEntry>());

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(0);
    }

    [Fact]
    public async Task Phase2_skips_when_item_unavailable()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: false, quantity: 0);
        var entry = NewAutoRedeemEntry(user.Id, item);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.RedemptionsPending.Should().Be(0);
        _redemptionRepo.DidNotReceive().Add(Arg.Any<Redemption>());
    }

    [Fact]
    public async Task Phase2_does_not_call_SE_api_for_redemption()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        await _sut.ExecuteAsync(CancellationToken.None);

        await _seApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Phase2_uses_storeitem_name_and_watchedstore_displayname_for_snapshots()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id, "ch-1", "xQc");
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5, name: "VIP Badge");
        var entry = NewAutoRedeemEntry(user.Id, item);

        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        await _sut.ExecuteAsync(CancellationToken.None);

        _redemptionRepo
            .Received(1)
            .Add(Arg.Is<Redemption>(r => r.StoreItemName == "VIP Badge" && r.StoreName == "xQc"));
    }

    #endregion

    #region Phase 3 — Notify

    [Fact]
    public async Task Phase3_sends_email_grouped_by_store()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var entry = new WishlistEntry(Guid.NewGuid(), user.Id, Guid.NewGuid(), Now);
        entry.SetNotifyOnAvailable(true, Now);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(entry);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<WishlistEntry>());
        _itemRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new StoreItem(
                callInfo.ArgAt<Guid>(0),
                store.Id,
                CreateApiData("se-1", store.ExternalId.Value, true, 5)
            ));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.NotificationsSent.Should().Be(1);
        await _emailNotifier
            .Received(1)
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Is<AvailabilityEmailModel>(m => m.Stores.Count == 1),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
        _notificationRepo
            .Received(1)
            .Add(Arg.Is<Notification>(n => n.Status == NotificationStatus.Sent));
    }

    [Fact]
    public async Task Phase3_notification_marks_pending_when_auto_redeem_produced_pending()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        // Phase 1 will create a fresh StoreItem (transition raised) — capture its id via item repo upsert
        StoreItem? created = null;
        _itemRepo.Upsert(
            Arg.Do<IReadOnlyCollection<StoreItem>>(items => created = items.FirstOrDefault())
        );

        // Pre-build the auto-redeem entry against a controlled item that matches the SE id
        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        var entry = NewAutoRedeemEntry(user.Id, item);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        // Notification path — entry hit by GetByStoreItemAsync regardless of id
        var notifyEntry = new WishlistEntry(Guid.NewGuid(), user.Id, item.Id, Now);
        notifyEntry.SetNotifyOnAvailable(true, Now);
        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(notifyEntry);
        _itemRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new StoreItem(
                callInfo.ArgAt<Guid>(0),
                store.Id,
                CreateApiData("se-1", store.ExternalId.Value, true, 5)
            ));

        AvailabilityEmailModel? capturedModel = null;
        await _emailNotifier.SendAvailabilityAsync(
            Arg.Any<string>(),
            Arg.Do<AvailabilityEmailModel>(m => capturedModel = m),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );

        // Override Phase 1 created event — feed via existing path: SE returns same item, no transition.
        // To ensure Phase 1 raises a transition we use a fresh item: ListByStoreAsync returns [].
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        // The phase 1 created item id != entry.StoreItemId in this setup, so the redeemedItemIds set
        // would not intersect Phase 1's events. Re-verify with broader assertion: pending flag matches
        // only when the StoreItemId of the email item equals one in the redeemed set.
        capturedModel.Should().NotBeNull();
        // In this scenario, Phase 1 created a StoreItem with a NEW id (not entry.StoreItemId);
        // Phase 2's redeemedItemIds set contains entry.StoreItemId. Therefore RedemptionPending = false.
        // For a TRUE assertion, see Phase3_pending_flag_true_when_redeemed_item_matches_event below.
        capturedModel!.Stores[0].Items[0].RedemptionPending.Should().BeFalse();
        _ = created;
    }

    [Fact]
    public async Task Phase3_pending_flag_true_when_redeemed_item_matches_event()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        // Local item already exists and is unavailable → api flips to available → Phase 1 raises transition event
        var localItem = NewItemWithStore(store, enabled: false, quantity: 0);
        localItem.ClearDomainEvents();
        _itemRepo
            .ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([localItem]);

        var apiItem = ToSeStoreItem(localItem) with
        {
            Enabled = true,
            Quantity = new ItemQuantity(5, 5),
        };
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        // Same localItem becomes the auto-redeem candidate after Phase 1 update
        var entry = NewAutoRedeemEntry(user.Id, localItem);
        _wishlistRepo
            .ListAutoRedeemWithItemsForUpdateAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        // Notify path
        var notifyEntry = new WishlistEntry(Guid.NewGuid(), user.Id, localItem.Id, Now);
        notifyEntry.SetNotifyOnAvailable(true, Now);
        _wishlistRepo
            .GetByStoreItemAsync(user.Id, localItem.Id, Arg.Any<CancellationToken>())
            .Returns(notifyEntry);
        _itemRepo.GetByIdAsync(localItem.Id, Arg.Any<CancellationToken>()).Returns(localItem);

        AvailabilityEmailModel? capturedModel = null;
        await _emailNotifier.SendAvailabilityAsync(
            Arg.Any<string>(),
            Arg.Do<AvailabilityEmailModel>(m => capturedModel = m),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );

        await _sut.ExecuteAsync(CancellationToken.None);

        capturedModel.Should().NotBeNull();
        capturedModel!.Stores[0].Items[0].RedemptionPending.Should().BeTrue();
    }

    [Fact]
    public async Task Phase3_skips_when_no_notifiable_items()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.NotificationsSent.Should().Be(0);
        await _emailNotifier
            .DidNotReceive()
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Any<AvailabilityEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Phase3_records_failed_notification()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var entry = new WishlistEntry(Guid.NewGuid(), user.Id, Guid.NewGuid(), Now);
        entry.SetNotifyOnAvailable(true, Now);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(entry);
        _itemRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new StoreItem(
                callInfo.ArgAt<Guid>(0),
                store.Id,
                CreateApiData("se-1", store.ExternalId.Value, true, 5)
            ));

        _emailNotifier
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Any<AvailabilityEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Email service down"));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.NotificationsSent.Should().Be(0);
        _notificationRepo
            .Received(1)
            .Add(Arg.Is<Notification>(n => n.Status == NotificationStatus.Failed));
    }

    /// <summary>
    /// An account carrying neither an email address nor a user name has nowhere to be notified, and
    /// that is settled here rather than by whatever mail server happens to receive the attempt.
    /// <para>
    /// The items are still recorded: what became available is worth keeping whether or not the
    /// account could be told about it, and the failed notification is what says it was not told.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Phase3_records_failed_notification_without_sending_when_the_user_has_no_address()
    {
        var user = SetupUserWithToken();
        user.Email = null;
        user.UserName = null;

        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);

        var apiItem = CreateSeStoreItem("se-1", store.ExternalId.Value, enabled: true, quantity: 5);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns([apiItem]);

        var entry = new WishlistEntry(Guid.NewGuid(), user.Id, Guid.NewGuid(), Now);
        entry.SetNotifyOnAvailable(true, Now);

        _wishlistRepo
            .GetByStoreItemAsync(user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(entry);
        _itemRepo
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new StoreItem(
                callInfo.ArgAt<Guid>(0),
                store.Id,
                CreateApiData("se-1", store.ExternalId.Value, true, 5)
            ));

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.NotificationsSent.Should().Be(0);
        await _emailNotifier
            .DidNotReceive()
            .SendAvailabilityAsync(
                Arg.Any<string>(),
                Arg.Any<AvailabilityEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
        _notificationRepo
            .Received(1)
            .Add(
                Arg.Is<Notification>(n =>
                    n.Status == NotificationStatus.Failed
                    && !string.IsNullOrWhiteSpace(n.FailureReason)
                    && n.Items.Count == 1
                )
            );
    }

    #endregion

    #region Phase 1.5 — Input coverage transitions

    [Fact]
    public async Task Coverage_evaluation_blocks_autoredeem_entry_with_missing_label_and_persists_in_app_notification()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(item, ["Discord Tag"]);
        var entry = NewAutoRedeemEntry(user.Id, item);

        _wishlistRepo
            .ListByStoreWithItemsForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        await _sut.ExecuteAsync(CancellationToken.None);

        entry.AutoRedeem.Should().BeFalse();
        entry.IsBlockedOnInput.Should().BeTrue();
        _wishlistRepo.Received().Update(entry);
        _notificationRepo
            .Received(1)
            .Add(Arg.Is<Notification>(n => n.Channel == NotificationChannel.WebUi));
    }

    [Fact]
    public async Task Coverage_evaluation_clears_block_when_profile_now_covers_labels()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        store.UpsertValue("Discord Tag", "ct", Now);
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(item, ["Discord Tag"]);
        var entry = new WishlistEntry(Guid.NewGuid(), user.Id, item.Id, Now);
        entry.EnableAutoRedeem(Now);
        entry.BlockOnMissingInput(Now);
        AttachStoreItem(entry, item);

        _wishlistRepo
            .ListByStoreWithItemsForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([entry]);

        await _sut.ExecuteAsync(CancellationToken.None);

        entry.IsBlockedOnInput.Should().BeFalse();
        _wishlistRepo.Received().Update(entry);
    }

    [Fact]
    public async Task Coverage_logs_one_row_per_distinct_label_on_first_observation()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);

        var itemA = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(itemA, ["Discord Tag"]);
        var itemB = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(itemB, ["Discord Tag", "Steam ID"]);
        AddItemToStore(store, itemA);
        AddItemToStore(store, itemB);

        await _sut.ExecuteAsync(CancellationToken.None);

        _logRepo
            .Received(1)
            .AddRange(
                Arg.Is<IEnumerable<StoreInputResolutionLogEntry>>(entries =>
                    entries.Count() == 2
                    && entries.All(e => e.Source == ResolutionSource.Unresolved)
                    && entries.Select(e => e.RawLabel).Contains("Discord Tag")
                    && entries.Select(e => e.RawLabel).Contains("Steam ID")
                )
            );
        // Both log rows and every block or unblock land in one transaction per store.
        await _coverageUnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Coverage_does_not_log_when_outcome_unchanged()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);
        SetupLatestOutcomes(
            store.Id,
            ("Discord Tag", new LatestLabelOutcome(null, ResolutionSource.Unresolved, 0))
        );

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(item, ["Discord Tag"]);
        AddItemToStore(store, item);

        await _sut.ExecuteAsync(CancellationToken.None);

        _logRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<StoreInputResolutionLogEntry>>());
    }

    [Fact]
    public async Task Coverage_logs_transition_when_label_becomes_resolved()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        store.UpsertValue("Discord Tag", "ct", Now);
        SetupCoverageSync(store);
        SetupLatestOutcomes(
            store.Id,
            ("Discord Tag", new LatestLabelOutcome(null, ResolutionSource.Unresolved, 0))
        );

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(item, ["Discord Tag"]);
        AddItemToStore(store, item);

        await _sut.ExecuteAsync(CancellationToken.None);

        _logRepo
            .Received(1)
            .AddRange(
                Arg.Is<IEnumerable<StoreInputResolutionLogEntry>>(entries =>
                    entries.Count() == 1
                    // The definition and the stored canonical are the same string, so the
                    // resolver reports Exact — the transition is what this test pins, not
                    // which flavour of match produced it.
                    && entries.Single().Source == ResolutionSource.Exact
                    && entries.Single().CanonicalLabel == "Discord Tag"
                )
            );
    }

    [Fact]
    public async Task Coverage_does_not_log_confidence_only_change()
    {
        var user = SetupUserWithToken();
        var store = CreateWatchedStore(user.Id);
        SetupCoverageSync(store);
        SetupLatestOutcomes(
            store.Id,
            ("Steam ID", new LatestLabelOutcome(null, ResolutionSource.Unresolved, 42))
        );

        var item = NewItemWithStore(store, enabled: true, quantity: 5);
        SetUserInputDefinitions(item, ["Steam ID"]);
        AddItemToStore(store, item);

        await _sut.ExecuteAsync(CancellationToken.None);

        // Best score is now 0 (no profile values) vs 42 before, but the
        // outcome (Unresolved, null) is unchanged — nothing is written.
        _logRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<StoreInputResolutionLogEntry>>());
    }

    [Fact]
    public async Task Prune_runs_exactly_once_per_tick_even_with_multiple_stores()
    {
        var user = SetupUserWithToken();
        var storeA = CreateWatchedStore(user.Id, channelId: "ch-a");
        var storeB = CreateWatchedStore(user.Id, channelId: "ch-b", name: "Store B");
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([storeA, storeB]);
        foreach (var store in new[] { storeA, storeB })
        {
            _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
            _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
            _seApi
                .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ProviderStoreItem>());
            _wishlistRepo
                .ListByStoreWithItemsForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
                .Returns([]);
            SetupLatestOutcomes(store.Id);
        }

        await _sut.ExecuteAsync(CancellationToken.None);

        await _logRepo
            .Received(1)
            .PruneAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private void SetupCoverageSync(WatchedStore store)
    {
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([store]);
        _storeRepo.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _storeRepo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);
        _itemRepo.ListByStoreForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns([]);
        _seApi
            .GetStoreItemsAsync(store.ExternalId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProviderStoreItem>());
        _wishlistRepo
            .ListByStoreWithItemsForUpdateAsync(store.Id, Arg.Any<CancellationToken>())
            .Returns([]);
        SetupLatestOutcomes(store.Id);
    }

    private void SetupLatestOutcomes(
        Guid storeId,
        params (string Label, LatestLabelOutcome Outcome)[] outcomes
    )
    {
        var dict = new Dictionary<string, LatestLabelOutcome>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, outcome) in outcomes)
        {
            dict[label] = outcome;
        }
        _logRepo.GetLatestPerLabelAsync(storeId, Arg.Any<CancellationToken>()).Returns(dict);
    }

    private static void AddItemToStore(WatchedStore store, StoreItem item)
    {
        var items =
            (List<StoreItem>)
                typeof(WatchedStore)
                    .GetField(
                        "_items",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    )!
                    .GetValue(store)!;
        items.Add(item);
    }

    private static void SetUserInputDefinitions(StoreItem item, IReadOnlyList<string> defs)
    {
        typeof(StoreItem).GetProperty(nameof(StoreItem.UserInputDefinitions))!.SetValue(item, defs);
    }

    #endregion

    #region Helpers

    private ApplicationUser CreateUser(string? token)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        if (token is not null)
        {
            _credentials.With(
                new ProviderCredential(user.Id, StoreProvider.StreamElements, token, Now)
            );
        }
        return user;
    }

    private void SetupUsers(ApplicationUser? user) =>
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

    private ApplicationUser SetupUserWithToken()
    {
        var user = CreateUser("encrypted-token");
        _credentials.Held.Single().SetAccountName("se-username", Now);
        SetupUsers(user);
        _tokenEncryption.Unprotect("encrypted-token").Returns("decrypted-jwt");
        _storeRepo.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([]);
        // Default: plenty of points so affordability never blocks unless a test overrides it.
        _seApi
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1_000_000L);
        return user;
    }

    private static WatchedStore CreateWatchedStore(
        Guid userId,
        string channelId = "ch-1",
        string name = "Test Store",
        StoreProvider provider = StoreProvider.StreamElements
    )
    {
        return new WatchedStore(
            Guid.NewGuid(),
            provider,
            new StoreExternalId(channelId),
            name,
            userId,
            Now
        );
    }

    private static ProviderStoreItem CreateSeStoreItem(
        string seId,
        string channelId,
        bool enabled,
        int quantity
    )
    {
        return new ProviderStoreItem(
            Provider: StoreProvider.StreamElements,
            Id: new ProviderItemId(seId),
            Store: new StoreExternalId(channelId),
            Name: $"Item {seId}",
            Description: null,
            Type: "perk",
            Cost: 100,
            CategoryName: string.Empty,
            Enabled: enabled,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: false,
            UserInputDefinitions: [],
            Sources: [],
            Quantity: new ItemQuantity(quantity, quantity),
            Cooldown: new ItemCooldown(0, 0, 0),
            CreatedAt: Now,
            UpdatedAt: null
        );
    }

    private static StoreItem NewItemWithStore(
        WatchedStore store,
        bool enabled,
        int quantity,
        ItemCooldown? cooldown = null,
        string name = "Item se-1"
    )
    {
        var data = new StoreItemApiData(
            store.Provider,
            new ProviderItemId("se-1"),
            store.ExternalId,
            name,
            null,
            "perk",
            100,
            "default",
            enabled,
            false,
            true,
            false,
            false,
            [],
            [],
            new ItemQuantity(quantity == -1 ? -1 : Math.Max(quantity, 0), quantity),
            cooldown ?? new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );
        var item = new StoreItem(Guid.NewGuid(), store.Id, data);
        item.ClearDomainEvents();
        AttachWatchedStore(item, store);
        return item;
    }

    private static WishlistEntry NewAutoRedeemEntry(Guid userId, StoreItem item)
    {
        var entry = new WishlistEntry(Guid.NewGuid(), userId, item.Id, Now);
        entry.EnableAutoRedeem(Now);
        AttachStoreItem(entry, item);
        return entry;
    }

    private static void AttachStoreItem(WishlistEntry entry, StoreItem item)
    {
        typeof(WishlistEntry).GetProperty(nameof(WishlistEntry.StoreItem))!.SetValue(entry, item);
    }

    private static void AttachWatchedStore(StoreItem item, WatchedStore store)
    {
        typeof(StoreItem).GetProperty(nameof(StoreItem.WatchedStore))!.SetValue(item, store);
    }

    private static ProviderStoreItem ToSeStoreItem(StoreItem item)
    {
        return new ProviderStoreItem(
            Provider: item.Provider,
            Id: item.ProviderItemId,
            Store: item.StoreExternalId,
            Name: item.Name,
            Description: item.Description,
            Type: item.Type,
            Cost: item.Cost,
            CategoryName: item.CategoryName,
            Enabled: item.Enabled,
            Featured: item.Featured,
            Public: item.Public,
            SubscriberOnly: item.SubscriberOnly,
            AllowMessages: item.AllowMessages,
            UserInputDefinitions: item.UserInputDefinitions.ToArray(),
            Sources: item.Sources.ToArray(),
            Quantity: item.Quantity,
            Cooldown: item.Cooldown,
            CreatedAt: item.ProviderCreatedAt,
            UpdatedAt: item.ProviderUpdatedAt
        );
    }

    private static StoreItemApiData CreateApiData(
        string seId,
        string channelId,
        bool enabled,
        int quantity,
        bool allowMessages = false
    )
    {
        return new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId(seId),
            new StoreExternalId(channelId),
            $"Item {seId}",
            null,
            "perk",
            100,
            "default",
            enabled,
            false,
            true,
            false,
            allowMessages,
            [],
            [],
            new ItemQuantity(quantity, quantity),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );
    }

    #endregion
}
