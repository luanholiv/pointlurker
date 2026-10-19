using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class GetWishlistEntryTests
{
    private readonly IWishlistEntryRepository _wishlistEntryRepository =
        Substitute.For<IWishlistEntryRepository>();
    private readonly IStoreItemRepository _storeItemRepository =
        Substitute.For<IStoreItemRepository>();
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly IRedemptionRepository _redemptionRepository =
        Substitute.For<IRedemptionRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private readonly ISecretProtector _messageProtector = Substitute.For<ISecretProtector>();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly GetWishlistEntry _sut;

    public GetWishlistEntryTests()
    {
        // Round-trip: Unprotect strips the "ct:" prefix that Protect adds (see write-side tests).
        _messageProtector
            .Unprotect(Arg.Any<string>())
            .Returns(call =>
            {
                var value = call.Arg<string>();
                return value.StartsWith("ct:", StringComparison.Ordinal) ? value[3..] : value;
            });
        _redemptionRepository
            .CountHoldingQuotaAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Dictionary<Guid, RedemptionQuotaUsage>());

        _sut = new GetWishlistEntry(
            _wishlistEntryRepository,
            _storeItemRepository,
            _storeRepository,
            _redemptionRepository,
            _resolver,
            _messageProtector
        );
    }

    [Fact]
    public async Task ExecuteAsync_returns_full_details_when_found()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(storeId, userInputDefinitions: ["email", "name"]);
        var entry = new WishlistEntry(Guid.NewGuid(), userId, storeItem.Id, Now);
        entry.SetRedemptionMessage("msg", allowMessages: true, Now);
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Test Store",
            userId,
            Now
        );
        store.UpsertValue("email", "ct1", Now);
        store.UpsertValue("name", "ct2", Now);

        _wishlistEntryRepository
            .GetByIdAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _storeRepository.GetProfileAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(entry.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(entry.Id);
        result.Value.ItemName.Should().Be("Test Item");
        result.Value.StoreName.Should().Be("Test Store");
        result.Value.IsBlocked.Should().BeFalse();
        result.Value.MissingLabels.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_unprotects_stored_message_before_returning()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(storeId);
        var entry = new WishlistEntry(Guid.NewGuid(), userId, storeItem.Id, Now);
        entry.SetRedemptionMessage("ct:GG", allowMessages: true, Now);
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Test Store",
            userId,
            Now
        );

        _wishlistEntryRepository
            .GetByIdAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _storeRepository.GetProfileAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(entry.Id);

        result.IsSuccess.Should().BeTrue();
        _messageProtector.Received(1).Unprotect("ct:GG");
        result.Value.RedemptionMessage.Should().Be("GG");
    }

    [Fact]
    public async Task ExecuteAsync_passes_null_message_through_without_unprotecting()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(storeId);
        var entry = new WishlistEntry(Guid.NewGuid(), userId, storeItem.Id, Now);
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Test Store",
            userId,
            Now
        );

        _wishlistEntryRepository
            .GetByIdAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _storeRepository.GetProfileAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(entry.Id);

        result.IsSuccess.Should().BeTrue();
        _messageProtector.DidNotReceive().Unprotect(Arg.Any<string>());
        result.Value.RedemptionMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_marks_blocked_when_auto_redeem_and_missing_labels()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(storeId, userInputDefinitions: ["email"]);
        var entry = new WishlistEntry(Guid.NewGuid(), userId, storeItem.Id, Now);
        entry.EnableAutoRedeem(Now);
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "S",
            userId,
            Now
        );

        _wishlistEntryRepository
            .GetByIdAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _storeRepository.GetProfileAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(entry.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsBlocked.Should().BeTrue();
        result.Value.MissingLabels.Should().BeEquivalentTo(["email"]);
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_entry_not_found()
    {
        _wishlistEntryRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.EntryNotFound);
    }

    private static StoreItem CreateStoreItem(
        Guid watchedStoreId,
        IReadOnlyList<string>? userInputDefinitions = null
    )
    {
        var data = new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            true,
            false,
            true,
            false,
            true,
            userInputDefinitions ?? [],
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );

        return new StoreItem(Guid.NewGuid(), watchedStoreId, data);
    }
}
