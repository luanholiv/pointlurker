using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class UpsertStoreProfileAliasTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpsertStoreProfileAlias _sut;

    public UpsertStoreProfileAliasTests()
    {
        _sut = new UpsertStoreProfileAlias(_storeRepository, _unitOfWork);
    }

    [Fact]
    public async Task Returns_not_found_when_store_missing()
    {
        _storeRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), "raw", "Canonical");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
    }

    [Fact]
    public async Task Persists_alias_via_repository_update()
    {
        var storeId = Guid.NewGuid();
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        _storeRepository.GetForUpdateAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(storeId, "twitchh usrname", "Twitch Username");

        result.IsSuccess.Should().BeTrue();
        store.InputAliases.Should().ContainSingle();
        store.InputAliases[0].RawLabel.Should().Be("twitchh usrname");
        store.InputAliases[0].LabelCanonical.Should().Be("Twitch Username");
        _storeRepository.Received(1).Update(store);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
