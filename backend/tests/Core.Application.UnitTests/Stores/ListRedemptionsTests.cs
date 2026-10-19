using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class ListRedemptionsTests
{
    private readonly IRedemptionRepository _redemptionRepo =
        Substitute.For<IRedemptionRepository>();
    private readonly ListRedemptions _sut;

    public ListRedemptionsTests()
    {
        _sut = new ListRedemptions(_redemptionRepo);
    }

    [Fact]
    public async Task Returns_dtos_with_snapshot_store_item_and_store_names()
    {
        var row = CreateRow("VIP Badge", "xQc");

        _redemptionRepo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(([row], 1));

        var result = await _sut.ExecuteAsync(new RedemptionFilter(null, null, null, null));

        result.TotalCount.Should().Be(1);
        result
            .Items.Should()
            .ContainSingle(dto => dto.StoreItemName == "VIP Badge" && dto.StoreName == "xQc");
    }

    [Fact]
    public async Task Does_not_call_store_item_or_store_repositories()
    {
        var storeItemRepo = Substitute.For<IStoreItemRepository>();
        var storeRepo = Substitute.For<IWatchedStoreRepository>();

        _redemptionRepo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(([CreateRow("Item", "Store")], 1));

        await _sut.ExecuteAsync(new RedemptionFilter(null, null, null, null));

        await storeItemRepo
            .DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await storeRepo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Filter_by_status_is_passed_to_repository()
    {
        _redemptionRepo
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.Status == RedemptionStatus.Pending),
                Arg.Any<int>(),
                Arg.Any<int>()
            )
            .Returns(([], 0));

        await _sut.ExecuteAsync(new RedemptionFilter(RedemptionStatus.Pending, null, null, null));

        await _redemptionRepo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.Status == RedemptionStatus.Pending),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task Filter_by_wishlist_entry_is_passed_to_repository()
    {
        var entryId = Guid.NewGuid();
        _redemptionRepo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(([], 0));

        await _sut.ExecuteAsync(new RedemptionFilter(null, entryId, null, null));

        await _redemptionRepo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.WishlistEntryId == entryId),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task Filter_by_date_range_is_passed_to_repository()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        _redemptionRepo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(([], 0));

        await _sut.ExecuteAsync(new RedemptionFilter(null, null, from, to));

        await _redemptionRepo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.From == from && f.To == to),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task Pagination_parameters_are_passed_to_repository()
    {
        _redemptionRepo.ListAsync(Arg.Any<RedemptionFilter>(), 3, 10).Returns(([], 0));

        await _sut.ExecuteAsync(new RedemptionFilter(null, null, null, null), offset: 3, limit: 10);

        await _redemptionRepo.Received(1).ListAsync(Arg.Any<RedemptionFilter>(), 3, 10);
    }

    [Fact]
    public async Task Total_count_reflects_full_result_set()
    {
        _redemptionRepo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(([], 42));

        var result = await _sut.ExecuteAsync(new RedemptionFilter(null, null, null, null));

        result.TotalCount.Should().Be(42);
    }

    // The names are snapshots the redemption took when it was created, which is why the history needs
    // no item or store join and this needs no aggregate to build a row.
    private static RedemptionListRow CreateRow(string storeItemName, string storeName) =>
        new(
            Guid.NewGuid(),
            RedemptionStatus.Pending,
            storeItemName,
            storeName,
            DateTimeOffset.UtcNow,
            SubmittedAt: null,
            SettledAt: null,
            FailureCode: null,
            FailureContext: null,
            PollFailures: 0,
            StoreProvider.StreamElements
        );
}
