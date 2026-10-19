using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;
using PointLurker.Presentation.WebApi.Search;

namespace Presentation.WebApi.UnitTests.Endpoints.Redemptions;

public sealed class RedemptionEndpointsTests
{
    private readonly ListRedemptions _listRedemptions;
    private readonly IRedemptionRepository _repo = Substitute.For<IRedemptionRepository>();

    public RedemptionEndpointsTests()
    {
        _listRedemptions = new ListRedemptions(_repo);
        _repo
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>())
            .Returns(((IReadOnlyList<RedemptionListRow>)[], 0));
    }

    [Fact]
    public async Task Returns_200_with_results()
    {
        _repo
            .ListAsync(Arg.Any<RedemptionFilter>(), 0, 20)
            .Returns(((IReadOnlyList<RedemptionListRow>)[], 3));

        var result = await ListAsync();

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_max_100()
    {
        await ListAsync(limit: "999");

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Any<RedemptionFilter>(),
                Arg.Is(0),
                Arg.Is(RedemptionEndpoints.MaxPageSize)
            );
    }

    [Fact]
    public async Task An_absent_limit_takes_the_documented_default()
    {
        await ListAsync();

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Any<RedemptionFilter>(),
                Arg.Is(0),
                Arg.Is(RedemptionEndpoints.DefaultPageSize)
            );
    }

    [Fact]
    public async Task Valid_status_filter_is_passed_to_use_case()
    {
        await ListAsync(status: "Submitted");

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.Status == RedemptionStatus.Submitted),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    /// <summary>
    /// An unparseable status is refused, not dropped. Dropping it would answer as though no filter
    /// had been asked for, handing a caller with a typo a correct-looking page of the wrong rows and
    /// no way to tell. The query is never issued.
    /// </summary>
    [Fact]
    public async Task Invalid_status_filter_is_rejected_rather_than_ignored()
    {
        var result = await ListAsync(status: "NotARealStatus");

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var fieldError = FirstFieldError(result);
        fieldError["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        fieldError["field"].Should().Be(SearchParameters.Status);

        await _repo
            .DidNotReceive()
            .ListAsync(Arg.Any<RedemptionFilter>(), Arg.Any<int>(), Arg.Any<int>());
    }

    #region Ordering

    [Fact]
    public async Task Ordering_defaults_to_attempted_time_most_recent_first()
    {
        await ListAsync();

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f =>
                    f.SortField == RedemptionSortField.AttemptedAt
                    && f.SortDirection == SortDirection.Desc
                ),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Theory]
    [InlineData("status", RedemptionSortField.Status)]
    [InlineData("storeItemName", RedemptionSortField.StoreItemName)]
    [InlineData("storeName", RedemptionSortField.StoreName)]
    [InlineData("attemptedAt", RedemptionSortField.AttemptedAt)]
    [InlineData("submittedAt", RedemptionSortField.SubmittedAt)]
    [InlineData("settledAt", RedemptionSortField.SettledAt)]
    public async Task Every_presented_column_is_sortable(string raw, RedemptionSortField expected)
    {
        await ListAsync(sort: raw);

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.SortField == expected),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task An_unknown_sort_field_is_rejected()
    {
        var result = await ListAsync(sort: "bogus");

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        FirstFieldError(result)["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
    }

    [Fact]
    public async Task An_unknown_direction_is_rejected()
    {
        var result = await ListAsync(dir: "sideways");

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion

    #region Envelope

    [Fact]
    public async Task The_response_expresses_its_slice_as_an_offset_and_a_limit()
    {
        _repo
            .ListAsync(Arg.Any<RedemptionFilter>(), 20, 20)
            .Returns(((IReadOnlyList<RedemptionListRow>)[], 57));

        var result = await ListAsync(offset: "20", limit: "20");

        var page = (RedemptionPageResponse)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        page.Offset.Should().Be(20);
        page.Limit.Should().Be(20);
        page.TotalCount.Should().Be(57);
    }

    [Fact]
    public async Task The_response_declares_exactly_the_shared_envelope()
    {
        typeof(RedemptionPageResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("Items", "TotalCount", "Offset", "Limit");

        await Task.CompletedTask;
    }

    #endregion

    [Fact]
    public async Task WishlistEntryId_filter_is_passed_to_use_case()
    {
        var entryId = Guid.NewGuid();

        await ListAsync(wishlistEntryId: entryId.ToString());

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.WishlistEntryId == entryId),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task Date_range_filters_are_passed_to_use_case()
    {
        var from = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-12-31T23:59:59Z");

        await ListAsync(from: "2026-01-01T00:00:00Z", to: "2026-12-31T23:59:59Z");

        await _repo
            .Received(1)
            .ListAsync(
                Arg.Is<RedemptionFilter>(f => f.From == from && f.To == to),
                Arg.Any<int>(),
                Arg.Any<int>()
            );
    }

    [Fact]
    public async Task A_malformed_date_is_rejected()
    {
        var result = await ListAsync(from: "not-a-date");

        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        FirstFieldError(result)["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
    }

    private Task<IResult> ListAsync(
        string? status = null,
        string? wishlistEntryId = null,
        string? from = null,
        string? to = null,
        string? sort = null,
        string? dir = null,
        string? offset = null,
        string? limit = null
    ) =>
        RedemptionEndpoints.ListRedemptionsAsync(
            new RedemptionListQuery(status, wishlistEntryId, from, to, sort, dir, offset, limit),
            _listRedemptions,
            CancellationToken.None
        );

    /// <summary>
    /// A rejected filter answers in the shared <c>validation.failed</c> envelope, so the code naming
    /// what actually broke is the one on the first <c>params.errors</c> entry.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> FirstFieldError(IResult result)
    {
        var problem = (ProblemDetails)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        problem.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);

        var wrapper = (IReadOnlyDictionary<string, object?>)problem.Extensions["params"]!;
        return ((IEnumerable<object?>)wrapper["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .First();
    }
}
