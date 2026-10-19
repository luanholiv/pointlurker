using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Stores;

public sealed class GetStoreDetailTests
{
    private static readonly DateTimeOffset Created = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly GetStoreDetail _sut;

    public GetStoreDetailTests()
    {
        _sut = new GetStoreDetail(_storeRepository);
    }

    [Fact]
    public async Task ExecuteAsync_returns_store_when_found()
    {
        var storeId = Guid.NewGuid();
        _storeRepository
            .GetDetailAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(Row(storeId));

        var result = await _sut.ExecuteAsync(storeId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(storeId);
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_not_found()
    {
        var storeId = Guid.NewGuid();

        _storeRepository
            .GetDetailAsync(storeId, Arg.Any<CancellationToken>())
            .Returns((WatchedStoreDetailRow?)null);

        var result = await _sut.ExecuteAsync(storeId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
    }

    /// <summary>
    /// The count is the store's own, not a filtered page's: the page uses it to tell "this store
    /// holds nothing" from "nothing matches your filters", which are different messages.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_reports_the_unfiltered_item_count()
    {
        var storeId = Guid.NewGuid();
        _storeRepository
            .GetDetailAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(Row(storeId) with { ItemCount = 40 });

        var result = await _sut.ExecuteAsync(storeId);

        result.Value.ItemCount.Should().Be(40);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_read_the_item_collection()
    {
        var storeId = Guid.NewGuid();
        _storeRepository
            .GetDetailAsync(storeId, Arg.Any<CancellationToken>())
            .Returns(Row(storeId));

        await _sut.ExecuteAsync(storeId);

        // GetByIdAsync is the read that pulls every item; this path must never use it, or the
        // endpoint split has bought nothing.
        await _storeRepository
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static WatchedStoreDetailRow Row(Guid storeId) =>
        new(
            storeId,
            "ch1",
            "Test Store",
            true,
            null,
            Created,
            0,
            null,
            StoreProvider.StreamElements
        );
}
