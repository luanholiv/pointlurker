using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class ActivateStoreTests
{
    private readonly IWatchedStoreRepository _repo = Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ActivateStore _sut;

    public ActivateStoreTests()
    {
        _sut = new ActivateStore(_repo, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_activates_store_and_persists()
    {
        var store = CreateStore();
        _repo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.IsSuccess.Should().BeTrue();
        store.IsActive.Should().BeTrue();
        _repo.Received(1).Update(store);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_returns_not_found_when_store_missing()
    {
        _repo
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
        _repo.DidNotReceive().Update(Arg.Any<WatchedStore>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_is_idempotent_for_already_active_store()
    {
        var store = CreateStore();
        _repo.GetForUpdateAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.IsSuccess.Should().BeTrue();
        store.IsActive.Should().BeTrue();
    }

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-test"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
