using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Stores;

/// <summary>
/// The row the query projects carries the same nine fields the DTO exposes, so what is worth pinning is
/// that the conversion keeps them in the right places — a field-for-field operator is exactly the kind
/// that silently transposes two same-typed neighbours.
/// </summary>
public sealed class RedemptionDtoTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly IRedemptionRepository _repo = Substitute.For<IRedemptionRepository>();
    private readonly ListRedemptions _sut;

    public RedemptionDtoTests()
    {
        _sut = new ListRedemptions(_repo);
    }

    [Fact]
    public async Task Dto_maps_all_non_nullable_fields()
    {
        var row = CreateRow("Badge", "Store");
        StubRows(row);

        var dto = (await ExecuteAsync()).Items.Single();

        dto.Id.Should().Be(row.Id);
        dto.StoreItemName.Should().Be("Badge");
        dto.StoreName.Should().Be("Store");
        dto.AttemptedAt.Should().Be(row.AttemptedAt);
        dto.Status.Should().Be(RedemptionStatus.Pending);
    }

    [Fact]
    public async Task Dto_maps_nullable_submitted_at_as_null_when_pending()
    {
        StubRows(CreateRow("Badge", "Store"));

        (await ExecuteAsync()).Items.Single().SubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task Dto_maps_nullable_settled_at_as_null_when_not_completed()
    {
        StubRows(CreateRow("Badge", "Store"));

        (await ExecuteAsync()).Items.Single().SettledAt.Should().BeNull();
    }

    [Fact]
    public async Task Dto_maps_null_failure_code_and_context_when_success()
    {
        StubRows(CreateRow("Badge", "Store"));

        var dto = (await ExecuteAsync()).Items.Single();

        dto.FailureCode.Should().BeNull();
        dto.FailureContext.Should().BeNull();
    }

    [Fact]
    public async Task Dto_carries_the_timestamps_and_failure_detail_of_a_finished_redemption()
    {
        var row = CreateRow("Badge", "Store") with
        {
            Status = RedemptionStatus.SubmissionFailed,
            SubmittedAt = Now.AddMinutes(1),
            SettledAt = Now.AddMinutes(2),
            FailureCode = SubmissionFailureCode.InsufficientPoints,
            FailureContext = "HTTP 400: not enough points",
        };
        StubRows(row);

        var dto = (await ExecuteAsync()).Items.Single();

        dto.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        dto.SubmittedAt.Should().Be(Now.AddMinutes(1));
        dto.SettledAt.Should().Be(Now.AddMinutes(2));
        dto.FailureCode.Should().Be(SubmissionFailureCode.InsufficientPoints);
        dto.FailureContext.Should().Be("HTTP 400: not enough points");
    }

    [Fact]
    public async Task Empty_result_maps_to_empty_list()
    {
        StubRows();

        var result = await ExecuteAsync();

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    private Task<RedemptionListResult> ExecuteAsync() =>
        _sut.ExecuteAsync(new RedemptionFilter(null, null, null, null));

    private void StubRows(params RedemptionListRow[] rows) =>
        _repo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns((rows, rows.Length));

    private static RedemptionListRow CreateRow(string storeItemName, string storeName) =>
        new(
            Guid.NewGuid(),
            RedemptionStatus.Pending,
            storeItemName,
            storeName,
            Now,
            SubmittedAt: null,
            SettledAt: null,
            FailureCode: null,
            FailureContext: null,
            PollFailures: 0,
            StoreProvider.StreamElements
        );
}
