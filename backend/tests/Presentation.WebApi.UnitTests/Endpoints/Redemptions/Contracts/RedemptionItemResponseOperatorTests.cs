using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Redemptions.Contracts;

public sealed class RedemptionItemResponseOperatorTests
{
    private static readonly DateTimeOffset Attempted = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Maps_full_field_set()
    {
        var dto = new RedemptionDto(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Status: RedemptionStatus.SubmissionFailed,
            StoreItemName: "Shoutout",
            StoreName: "xQc Store",
            AttemptedAt: Attempted,
            SubmittedAt: Attempted.AddSeconds(5),
            SettledAt: Attempted.AddSeconds(9),
            FailureCode: SubmissionFailureCode.ProviderApiBadRequest,
            FailureContext: "input missing",
            PollFailures: 3,
            Provider: StoreProvider.StreamElements
        );

        var response = (RedemptionItemResponse)dto;

        response
            .Should()
            .BeEquivalentTo(
                new RedemptionItemResponse(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Status: "SubmissionFailed",
                    StoreItemName: "Shoutout",
                    StoreName: "xQc Store",
                    AttemptedAt: Attempted,
                    SubmittedAt: Attempted.AddSeconds(5),
                    SettledAt: Attempted.AddSeconds(9),
                    FailureCode: ErrorCodes.RedemptionFailure.ProviderApiBadRequest,
                    FailureContext: "input missing",
                    PollFailures: 3,
                    Provider: "streamelements"
                )
            );
    }

    [Theory]
    [InlineData(RedemptionStatus.Pending, "Pending")]
    [InlineData(RedemptionStatus.Submitted, "Submitted")]
    [InlineData(RedemptionStatus.Completed, "Completed")]
    [InlineData(RedemptionStatus.SubmissionFailed, "SubmissionFailed")]
    [InlineData(RedemptionStatus.CompletionTimedOut, "CompletionTimedOut")]
    public void Serializes_the_status_as_its_name(RedemptionStatus status, string expected)
    {
        var response = (RedemptionItemResponse)(MinimalDto() with { Status = status });

        response.Status.Should().Be(expected);
    }

    [Fact]
    public void Serializes_the_failure_code_as_its_error_code()
    {
        var dto = MinimalDto() with { FailureCode = SubmissionFailureCode.InputCoverageLost };

        var response = (RedemptionItemResponse)dto;

        response
            .FailureCode.Should()
            .Be(
                ErrorCodes.RedemptionFailure.InputCoverageLost,
                "the reader resolves the code through its catalog; handing it the stored ordinal "
                    + "would make it keep a copy of the enum to translate"
            );
    }

    [Fact]
    public void Maps_a_pending_redemption_with_no_timestamps_or_failure()
    {
        var response = (RedemptionItemResponse)MinimalDto();

        response.SubmittedAt.Should().BeNull();
        response.SettledAt.Should().BeNull();
        response.FailureCode.Should().BeNull();
        response.FailureContext.Should().BeNull();
        response.PollFailures.Should().Be(0);
    }

    private static RedemptionDto MinimalDto() =>
        new(
            Id: Guid.NewGuid(),
            Status: RedemptionStatus.Pending,
            StoreItemName: "Shoutout",
            StoreName: "xQc Store",
            AttemptedAt: Attempted,
            SubmittedAt: null,
            SettledAt: null,
            FailureCode: null,
            FailureContext: null,
            PollFailures: 0,
            Provider: StoreProvider.StreamElements
        );
}
