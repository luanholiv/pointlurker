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

public sealed class UpdateWishlistEntryTests
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
    private readonly UpdateWishlistEntry _sut;

    public UpdateWishlistEntryTests()
    {
        _messageProtector.Protect(Arg.Any<string>()).Returns(call => $"ct:{call.Arg<string>()}");
        _sut = new UpdateWishlistEntry(
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
    public async Task ExecuteAsync_updates_existing_entry()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        var storeItem = CreateStoreItem(entry.StoreItemId, allowMessages: true);

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(entry.StoreItemId, Arg.Any<CancellationToken>())
            .Returns(storeItem);

        var result = await _sut.ExecuteAsync(
            entry.Id,
            false,
            false,
            desiredRedemptions: 1,
            "new message"
        );

        result.IsSuccess.Should().BeTrue();
        entry.NotifyOnAvailable.Should().BeFalse();
        entry.RedemptionMessage.Should().Be("ct:new message");
        _wishlistEntryRepository.Received(1).Update(entry);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(null)]
    public async Task ExecuteAsync_re_arms_a_spent_entry_by_raising_the_desired_count(int? desired)
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        var storeItem = CreateStoreItem(entry.StoreItemId, allowMessages: true);

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(entry.StoreItemId, Arg.Any<CancellationToken>())
            .Returns(storeItem);

        var result = await _sut.ExecuteAsync(
            entry.Id,
            true,
            false,
            desiredRedemptions: desired,
            null
        );

        result.IsSuccess.Should().BeTrue();
        entry.DesiredRedemptions.Should().Be(desired);
    }

    [Fact]
    public async Task ExecuteAsync_refuses_a_message_for_an_item_that_disallows_messages()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        var storeItem = CreateStoreItem(entry.StoreItemId, allowMessages: false);

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(entry.StoreItemId, Arg.Any<CancellationToken>())
            .Returns(storeItem);

        var result = await _sut.ExecuteAsync(
            entry.Id,
            true,
            false,
            desiredRedemptions: 1,
            "not allowed"
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.MessagesNotSupported);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_treats_an_empty_message_as_none_for_an_item_that_disallows_messages()
    {
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        var storeItem = CreateStoreItem(entry.StoreItemId, allowMessages: false);

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(entry.StoreItemId, Arg.Any<CancellationToken>())
            .Returns(storeItem);

        var result = await _sut.ExecuteAsync(entry.Id, true, false, desiredRedemptions: 1, "");

        result.IsSuccess.Should().BeTrue();
        entry.RedemptionMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_returns_not_found_when_entry_missing()
    {
        _wishlistEntryRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WishlistEntry?)null);

        var result = await _sut.ExecuteAsync(
            Guid.NewGuid(),
            true,
            false,
            desiredRedemptions: 1,
            null
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.EntryNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_auto_redeem_when_profile_missing()
    {
        var storeId = Guid.NewGuid();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        var storeItem = CreateStoreItem(
            entry.StoreItemId,
            userInputDefinitions: ["email"],
            storeId: storeId
        );
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "S",
            entry.UserId,
            DateTimeOffset.UtcNow
        );

        _wishlistEntryRepository
            .GetForUpdateAsync(entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);
        _storeItemRepository
            .GetByIdAsync(entry.StoreItemId, Arg.Any<CancellationToken>())
            .Returns(storeItem);
        _watchedStoreRepository
            .GetProfileAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(store);

        var result = await _sut.ExecuteAsync(entry.Id, true, true, desiredRedemptions: 1, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Wishlist.MissingInputCoverage);
    }

    private static StoreItem CreateStoreItem(
        Guid id,
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

        return new StoreItem(id, storeId ?? Guid.NewGuid(), data);
    }
}
