using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class DeactivateStoreTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeactivateStore _sut;

    public DeactivateStoreTests()
    {
        _sut = new DeactivateStore(_storeRepository, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_deactivates_existing_store()
    {
        var storeId = Guid.NewGuid();
        var store = new WatchedStore(
            storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch1"),
            "Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );

        _storeRepository.GetForUpdateAsync(storeId, Arg.Any<CancellationToken>()).Returns(store);

        var result = await _sut.ExecuteAsync(storeId);

        result.IsSuccess.Should().BeTrue();
        store.IsActive.Should().BeFalse();
        _storeRepository.Received(1).Update(store);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_not_found()
    {
        _storeRepository
            .GetForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
        _storeRepository.DidNotReceive().Update(Arg.Any<WatchedStore>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
