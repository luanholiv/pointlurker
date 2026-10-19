using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

public sealed class RedemptionResponseOperatorTests
{
    [Fact]
    public void Maps_full_field_set()
    {
        var dto = new RedemptionResponse
        {
            Id = "redemption-1",
            Channel = "ch-1",
            Item = "item-1",
            Completed = true,
            Input = ["email", "twitter"],
            Message = "thanks",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var redemption = (ProviderRedemptionResult)dto;

        redemption
            .Should()
            .Be(new ProviderRedemptionResult.Accepted("redemption-1", true, FulfillmentCode: null));
    }

    [Fact]
    public void Pending_status_propagates_completed_false()
    {
        var dto = new RedemptionResponse
        {
            Id = "r-2",
            Channel = "ch-1",
            Item = "item-1",
            Completed = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var redemption = (ProviderRedemptionResult)dto;

        redemption
            .Should()
            .BeOfType<ProviderRedemptionResult.Accepted>()
            .Which.Completed.Should()
            .BeFalse();
    }
}
