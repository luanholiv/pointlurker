using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class AddWishlistEntryTests
{
    private readonly IStoreItemRepository _storeItemRepository =
        Substitute.For<IStoreItemRepository>();
    private readonly IWishlistEntryRepository _wishlistEntryRepository =
        Substitute.For<IWishlistEntryRepository>();
    private readonly IWatchedStoreRepository _watchedStoreRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private readonly ISecretProtector _messageProtector = Substitute.For<ISecretProtector>();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AddWishlistEntry _sut;

    public AddWishlistEntryTests()
    {
        _messageProtector.Protect(Arg.Any<string>()).Returns(call => $"ct:{call.Arg<string>()}");
        _sut = new AddWishlistEntry(
            _storeItemRepository,
            _wishlistEntryRepository,
            _watchedStoreRepository,
            _resolver,
            _messageProtector,
            _unitOfWork,
            _timeProvider
        );
    }

    [Fact]
    public async Task ExecuteAsync_succeeds_when_item_exists_and_not_duplicated()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem();

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            null
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Entry.AutoRedeem.Should().BeFalse();
        result.Value.Entry.DesiredRedemptions.Should().Be(1);
        _wishlistEntryRepository.Received(1).Add(Arg.Any<WishlistEntry>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(null)]
    public async Task ExecuteAsync_stores_the_desired_redemption_count(int? desired)
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem();
        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: desired,
            null
        );

        result.Value.Entry.DesiredRedemptions.Should().Be(desired);
    }

    [Fact]
    public async Task ExecuteAsync_protects_non_empty_message_before_storing()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem(allowMessages: true);
        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            "GG"
        );

        result.IsSuccess.Should().BeTrue();
        _messageProtector.Received(1).Protect("GG");
        result.Value.Entry.RedemptionMessage.Should().Be("ct:GG");
    }

    [Fact]
    public async Task ExecuteAsync_stores_null_message_without_protecting()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem(allowMessages: true);
        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            null
        );

        result.IsSuccess.Should().BeTrue();
        _messageProtector.DidNotReceive().Protect(Arg.Any<string>());
        result.Value.Entry.RedemptionMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_accepts_message_at_exactly_max_length()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem(allowMessages: true);
        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);
        var atLimit = new string('x', WishlistEntry.RedemptionMessageMaxLength);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            atLimit
        );

        result.IsSuccess.Should().BeTrue();
        _messageProtector.Received(1).Protect(atLimit);
    }

    [Fact]
    public async Task ExecuteAsync_returns_conflict_when_already_wishlisted()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem();
        var existing = new WishlistEntry(
            Guid.NewGuid(),
            userId,
            storeItem.Id,
            DateTimeOffset.UtcNow
        );

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            null
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.AlreadyExists);
    }

    [Fact]
    public async Task ExecuteAsync_returns_not_found_when_item_missing()
    {
        _storeItemRepository
            .GetByIdWithStoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((StoreItem?)null);

        var result = await _sut.ExecuteAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            true,
            false,
            desiredRedemptions: 1,
            null
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.ItemNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_auto_redeem_when_profile_missing_labels()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(
            userInputDefinitions: ["Twitch Username"],
            storeId: storeId
        );
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "S",
            userId,
            DateTimeOffset.UtcNow
        );

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);
        _watchedStoreRepository
            .GetProfileAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(store);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            true,
            desiredRedemptions: 1,
            null
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.MissingInputCoverage);
        result
            .Error.Params!["missingLabels"]
            .Should()
            .BeAssignableTo<IReadOnlyList<string>>()
            .Which.Should()
            .Equal("Twitch Username");
    }

    [Fact]
    public async Task ExecuteAsync_succeeds_auto_redeem_when_profile_covers_labels()
    {
        var userId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var storeItem = CreateStoreItem(
            userInputDefinitions: ["Twitch Username"],
            storeId: storeId
        );
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "S",
            userId,
            DateTimeOffset.UtcNow
        );
        store.UpsertValue("Twitch Username", "ciphertext", DateTimeOffset.UtcNow);

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);
        _watchedStoreRepository
            .GetProfileAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(store);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            true,
            desiredRedemptions: 1,
            null
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Entry.AutoRedeem.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_refuses_a_message_for_an_item_that_disallows_messages()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem(allowMessages: false);

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            "not allowed"
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.MessagesNotSupported);
        _wishlistEntryRepository.DidNotReceive().Add(Arg.Any<WishlistEntry>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A form posts the whole body, so an item without messages still arrives with
    /// <c>redemptionMessage: ""</c>. That is the absence of a message, not a message.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_treats_an_empty_message_as_none_for_an_item_that_disallows_messages()
    {
        var userId = Guid.NewGuid();
        var storeItem = CreateStoreItem(allowMessages: false);

        _storeItemRepository
            .GetByIdWithStoreAsync(storeItem.Id, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _wishlistEntryRepository
            .GetByStoreItemAsync(userId, storeItem.Id, Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            userId,
            storeItem.Id,
            true,
            false,
            desiredRedemptions: 1,
            ""
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Entry.RedemptionMessage.Should().BeNull();
        _messageProtector.DidNotReceive().Protect(Arg.Any<string>());
    }

    private static StoreItem CreateStoreItem(
        IReadOnlyList<string>? userInputDefinitions = null,
        bool allowMessages = false,
        Guid? storeId = null
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
            allowMessages,
            userInputDefinitions ?? [],
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            DateTimeOffset.UtcNow,
            null,
            DateTimeOffset.UtcNow
        );

        return new StoreItem(Guid.NewGuid(), storeId ?? Guid.NewGuid(), data);
    }
}
