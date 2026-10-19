using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Redemptions.Contracts;

public sealed class RedemptionPageResponseOperatorTests
{
    [Fact]
    public void Maps_items_and_total_count()
    {
        var result = new RedemptionListResult(
            [Dto("First"), Dto("Second")],
            TotalCount: 42,
            Offset: 0,
            Limit: 20
        );

        var response = (RedemptionPageResponse)result;

        response.TotalCount.Should().Be(42);
        response.Items.Should().HaveCount(2);
        response.Items.Select(i => i.StoreItemName).Should().Equal("First", "Second");
    }

    /// <summary>
    /// The slice bounds travel on the result now rather than being patched in afterwards: the use
    /// case is handed the clamped values and reports what it actually asked the database for, so
    /// the operator has nothing left to leave blank.
    /// </summary>
    [Fact]
    public void Carries_the_slice_bounds_the_query_used()
    {
        var response = (RedemptionPageResponse)
            new RedemptionListResult([], TotalCount: 57, Offset: 40, Limit: 20);

        response.Offset.Should().Be(40);
        response.Limit.Should().Be(20);
    }

    [Fact]
    public void Maps_an_empty_page()
    {
        var response = (RedemptionPageResponse)
            new RedemptionListResult([], TotalCount: 0, Offset: 0, Limit: 20);

        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public void Declares_exactly_the_shared_envelope()
    {
        typeof(RedemptionPageResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("Items", "TotalCount", "Offset", "Limit");
    }

    private static RedemptionDto Dto(string storeItemName) =>
        new(
            Id: Guid.NewGuid(),
            Status: RedemptionStatus.Pending,
            StoreItemName: storeItemName,
            StoreName: "xQc Store",
            AttemptedAt: DateTimeOffset.UnixEpoch,
            SubmittedAt: null,
            SettledAt: null,
            FailureCode: null,
            FailureContext: null,
            PollFailures: 0,
            Provider: StoreProvider.StreamElements
        );
}
