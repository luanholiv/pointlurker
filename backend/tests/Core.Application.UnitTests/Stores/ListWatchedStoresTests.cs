using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Stores;

public sealed class ListWatchedStoresTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly ListWatchedStores _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public ListWatchedStoresTests()
    {
        _sut = new ListWatchedStores(_storeRepository);
    }

    [Fact]
    public async Task ExecuteAsync_returns_stores_for_user()
    {
        Returns([Row("Store 1"), Row("Store 2")], total: 2);

        var result = await ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_returns_empty_when_no_stores()
    {
        Returns([], total: 0);

        var result = await ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_passes_the_filter_to_the_repository()
    {
        Returns([], total: 0);

        await ExecuteAsync(new WatchedStoreFilter("xqc", IsActive: true));

        await _storeRepository
            .Received(1)
            .ListByUserAsync(
                _userId,
                Arg.Is<WatchedStoreFilter>(filter =>
                    filter.TextContains == "xqc" && filter.IsActive == true
                ),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_scopes_the_read_to_the_calling_user()
    {
        Returns([], total: 0);

        await ExecuteAsync();

        await _storeRepository
            .Received(1)
            .ListByUserAsync(
                _userId,
                Arg.Any<WatchedStoreFilter>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_echoes_the_slice_bounds_it_was_given()
    {
        Returns([], total: 57);

        var result = await ExecuteAsync(offset: 20, limit: 10);

        result.Value.Offset.Should().Be(20);
        result.Value.Limit.Should().Be(10);
        result.Value.TotalCount.Should().Be(57, "the total counts matches, not returned rows");
    }

    private Task<Result<WatchedStoreListResult, StoreError>> ExecuteAsync(
        WatchedStoreFilter? filter = null,
        int offset = 0,
        int limit = 200
    ) => _sut.ExecuteAsync(_userId, filter ?? new WatchedStoreFilter(null, null), offset, limit);

    private void Returns(IReadOnlyList<WatchedStoreListRow> rows, int total) =>
        _storeRepository
            .ListByUserAsync(
                Arg.Any<Guid>(),
                Arg.Any<WatchedStoreFilter>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((rows, total));

    private static WatchedStoreListRow Row(string displayName) =>
        new(Guid.NewGuid(), "ch", displayName, true, null, 0, null, StoreProvider.StreamElements);
}
