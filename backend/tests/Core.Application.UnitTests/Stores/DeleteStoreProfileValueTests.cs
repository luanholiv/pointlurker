using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class DeleteStoreProfileValueTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeleteStoreProfileValue _sut;

    public DeleteStoreProfileValueTests()
    {
        _sut = new DeleteStoreProfileValue(_storeRepository, _unitOfWork);
    }

    [Fact]
    public async Task Deletes_an_existing_value()
    {
        var store = CreateStore();
        store.UpsertValue("Steam ID", "ct", DateTimeOffset.UtcNow);
        _storeRepository.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id, "Steam ID");

        result.IsSuccess.Should().BeTrue();
        store.InputValues.Should().BeEmpty();
        _storeRepository.Received(1).Update(store);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_is_idempotent_when_value_absent()
    {
        var store = CreateStore();
        _storeRepository.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id, "Steam ID");

        result.IsSuccess.Should().BeTrue();
        // Nothing changed, so nothing is written — the caller still sees 204.
        _storeRepository.DidNotReceive().Update(Arg.Any<WatchedStore>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Matches_the_label_the_way_the_aggregate_keys_it()
    {
        var store = CreateStore();
        store.UpsertValue("Discord tag / handle", "ct", DateTimeOffset.UtcNow);
        _storeRepository.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id, "  discord   TAG / handle ");

        result.IsSuccess.Should().BeTrue();
        store.InputValues.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_not_found_when_store_missing()
    {
        _storeRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), "Steam ID");

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
