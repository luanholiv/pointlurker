using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class RefreshStoreChannelInfoTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly IWatchedStoreRepository _storeRepo = Substitute.For<IWatchedStoreRepository>();
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RefreshStoreChannelInfo _sut;

    public RefreshStoreChannelInfoTests()
    {
        _sut = new RefreshStoreChannelInfo(
            _storeRepo,
            StoreProviderClients.FactoryFor(_seApi),
            _unitOfWork,
            NullLogger<RefreshStoreChannelInfo>.Instance
        );
    }

    private static WatchedStore CreateStore(string channelId, string displayName) =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId(channelId),
            displayName,
            Guid.NewGuid(),
            Now
        );

    private void WithActiveStores(params WatchedStore[] stores) =>
        _storeRepo.ListActiveForUpdateAsync(Arg.Any<CancellationToken>()).Returns(stores);

    [Fact]
    public async Task Refreshes_display_name_and_avatar_for_every_active_store()
    {
        var first = CreateStore("gaules", "Old Gaules");
        var second = CreateStore("alanzoka", "Old Alanzoka");
        WithActiveStores(first, second);
        _seApi
            .LookupStoreAsync("gaules", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("id-1"),
                    "gaules",
                    "Gaules",
                    "https://cdn.example.com/gaules.png"
                )
            );
        _seApi
            .LookupStoreAsync("alanzoka", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("id-2"),
                    "alanzoka",
                    "Alanzoka",
                    "https://cdn.example.com/alanzoka.png"
                )
            );

        await _sut.ExecuteAsync();

        first.DisplayName.Should().Be("Gaules");
        first.AvatarUrl.Should().Be("https://cdn.example.com/gaules.png");
        second.DisplayName.Should().Be("Alanzoka");
        second.AvatarUrl.Should().Be("https://cdn.example.com/alanzoka.png");
        _storeRepo.Received(1).Update(first);
        _storeRepo.Received(1).Update(second);
    }

    [Fact]
    public async Task Leaves_alone_a_store_whose_provider_has_no_channel_metadata()
    {
        var store = CreateStore("gaupoints", "GauPoints");
        WithActiveStores(store);
        _seApi.Capabilities.Returns(
            StoreProviderClients.StreamElementsLike with
            {
                ProvidesChannelMetadata = false,
            }
        );

        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commits_once_for_the_whole_pass()
    {
        WithActiveStores(CreateStore("gaules", "Gaules"), CreateStore("alanzoka", "Alanzoka"));
        _seApi
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProviderStoreLookup(new StoreExternalId("id"), "channel", "Channel"));

        await _sut.ExecuteAsync();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task One_store_failing_leaves_its_channel_info_and_refreshes_the_rest()
    {
        var failing = CreateStore("gaules", "Old Gaules");
        var healthy = CreateStore("alanzoka", "Old Alanzoka");
        WithActiveStores(failing, healthy);
        _seApi
            .LookupStoreAsync("gaules", Arg.Any<CancellationToken>())
            .Returns<ProviderStoreLookup>(_ =>
                throw new ProviderApiException(500, "Server Error", "boom")
            );
        _seApi
            .LookupStoreAsync("alanzoka", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("id-2"),
                    "alanzoka",
                    "Alanzoka",
                    "https://cdn.example.com/alanzoka.png"
                )
            );

        await _sut.ExecuteAsync();

        failing.DisplayName.Should().Be("Old Gaules");
        failing.AvatarUrl.Should().BeNull();
        healthy.DisplayName.Should().Be("Alanzoka");
        healthy.AvatarUrl.Should().Be("https://cdn.example.com/alanzoka.png");
        _storeRepo.DidNotReceive().Update(failing);
        _storeRepo.Received(1).Update(healthy);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_stores_means_no_lookup_and_nothing_to_commit()
    {
        WithActiveStores();

        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nothing_is_committed_when_every_lookup_fails()
    {
        WithActiveStores(CreateStore("gaules", "Gaules"));
        _seApi
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProviderStoreLookup>(_ =>
                throw new ProviderApiException(500, "Server Error", "boom")
            );

        await _sut.ExecuteAsync();

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
