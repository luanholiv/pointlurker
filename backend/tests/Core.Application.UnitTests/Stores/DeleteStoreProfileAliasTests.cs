using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class DeleteStoreProfileAliasTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeleteStoreProfileAlias _sut;

    public DeleteStoreProfileAliasTests()
    {
        _sut = new DeleteStoreProfileAlias(_storeRepository, _unitOfWork);
    }

    [Fact]
    public async Task Deletes_an_existing_alias()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "ct", DateTimeOffset.UtcNow);
        store.UpsertAlias("twitchh usrname", "Twitch Username");
        _storeRepository.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id, "twitchh usrname");

        result.IsSuccess.Should().BeTrue();
        store.InputAliases.Should().BeEmpty();
        // Detaching the alias leaves the value untouched — only the mapping goes.
        store.InputValues.Should().ContainSingle();
        _storeRepository.Received(1).Update(store);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_alias_is_idempotent_when_absent()
    {
        var store = CreateStore();
        _storeRepository.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id, "twitchh usrname");

        result.IsSuccess.Should().BeTrue();
        _storeRepository.DidNotReceive().Update(Arg.Any<WatchedStore>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_not_found_when_store_missing()
    {
        _storeRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), "twitchh usrname");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
    }

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
