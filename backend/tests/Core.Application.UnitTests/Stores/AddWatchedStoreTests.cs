using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class AddWatchedStoreTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AddWatchedStore _sut;

    public AddWatchedStoreTests()
    {
        _sut = new AddWatchedStore(_storeRepository, _resolver, _unitOfWork, _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_persists_store_with_items()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("channel123");
        var itemsData = new[]
        {
            CreateItemData("item1", channelId),
            CreateItemData("item2", channelId),
        };

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Test Store",
            itemsData
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.DisplayName.Should().Be("Test Store");
        result.Value.Store.ExternalId.Should().Be(channelId);
        result.Value.Store.UserId.Should().Be(userId);
        result.Value.Store.IsActive.Should().BeTrue();
        result.Value.Store.LastSyncedAt.Should().Be(Now);
        result.Value.Store.Items.Should().HaveCount(2);
        result.Value.MissingLabels.Should().BeEmpty();

        _storeRepository.Received(1).Add(Arg.Any<WatchedStore>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_returns_the_persisted_aggregate_without_reloading_it()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("channel123");
        var itemsData = new[] { CreateItemData("item1", channelId) };

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Test Store",
            itemsData
        );

        // The result is the instance handed to AddAsync, not a second copy read back out of the
        // database. Nothing about a new store is database-generated, so the reload was pure cost.
        var persisted = _storeRepository
            .ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IWatchedStoreRepository.Add))
            .GetArguments()[0];

        result.Value.Store.Should().BeSameAs(persisted);

        await _storeRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _storeRepository
            .DidNotReceive()
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_persists_avatar_url_from_lookup()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("channel-avatar");

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        _storeRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new WatchedStore(
                callInfo.ArgAt<Guid>(0),
                StoreProvider.StreamElements,
                channelId,
                "Test Store",
                userId,
                DateTimeOffset.UtcNow,
                avatarUrl: "https://cdn.example.com/avatar.png"
            ));

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Test Store",
            [],
            avatarUrl: "https://cdn.example.com/avatar.png"
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");
    }

    [Fact]
    public async Task ExecuteAsync_leaves_avatar_null_when_not_provided()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("channel-no-avatar");

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        _storeRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new WatchedStore(
                callInfo.ArgAt<Guid>(0),
                StoreProvider.StreamElements,
                channelId,
                "Test Store",
                userId,
                DateTimeOffset.UtcNow
            ));

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Test Store",
            []
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_duplicate_channel_exists()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("channel123");

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Dupe",
            []
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.AlreadyExists);
    }

    [Fact]
    public async Task ExecuteAsync_creates_store_with_empty_items_when_no_items_returned()
    {
        var userId = Guid.NewGuid();
        var channelId = new StoreExternalId("emptystore");

        _storeRepository
            .ExistsByUserAndExternalIdAsync(
                userId,
                StoreProvider.StreamElements,
                channelId,
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        _storeRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new WatchedStore(
                callInfo.ArgAt<Guid>(0),
                StoreProvider.StreamElements,
                channelId,
                "Empty Store",
                userId,
                DateTimeOffset.UtcNow
            ));

        var result = await _sut.ExecuteAsync(
            userId,
            StoreProvider.StreamElements,
            channelId,
            "Empty Store",
            []
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Store.Items.Should().BeEmpty();
        _storeRepository.Received(1).Add(Arg.Any<WatchedStore>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static StoreItemApiData CreateItemData(string seId, StoreExternalId channelId)
    {
        return new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId(seId),
            channelId,
            $"Item {seId}",
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
        );
    }
}
