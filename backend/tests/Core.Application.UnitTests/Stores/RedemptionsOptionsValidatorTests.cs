using FluentAssertions;
using PointLurker.Core.Application.Stores;

namespace Core.Application.UnitTests.Stores;

public sealed class RedemptionsOptionsValidatorTests
{
    private readonly RedemptionsOptionsValidator _validator = new();

    [Fact]
    public void Valid_options_returns_success()
    {
        var options = new RedemptionsOptions
        {
            PollingCronExpression = "* * * * *",
            CompletionTimeoutHours = 24,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-a-cron")]
    [InlineData("60 * * * *")]
    [InlineData("* * * * * * *")]
    public void Invalid_cron_expression_returns_failure(string invalidCron)
    {
        var options = new RedemptionsOptions { PollingCronExpression = invalidCron };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("PollingCronExpression"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Non_positive_timeout_returns_failure(int timeoutHours)
    {
        var options = new RedemptionsOptions
        {
            PollingCronExpression = "* * * * *",
            CompletionTimeoutHours = timeoutHours,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("CompletionTimeoutHours"));
    }

    /// <summary>
    /// A non-positive threshold would escalate on the first failed status check, or never — either
    /// way the operator loses the signal that separates a blip from a wedged redemption.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Non_positive_poll_failure_alert_threshold_returns_failure(int threshold)
    {
        var options = new RedemptionsOptions
        {
            PollingCronExpression = "* * * * *",
            CompletionTimeoutHours = 24,
            PollFailureAlertThreshold = threshold,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("PollFailureAlertThreshold"));
    }

    /// <summary>
    /// A non-positive sweep interval would busy-loop the pending query, so it has to fail startup
    /// rather than be clamped silently.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Non_positive_retry_sweep_interval_returns_failure(int minutes)
    {
        var options = new RedemptionsOptions
        {
            PollingCronExpression = "* * * * *",
            CompletionTimeoutHours = 24,
            RetrySweepIntervalMinutes = minutes,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("RetrySweepIntervalMinutes"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_fulfillment_sweep_interval_returns_failure(int minutes)
    {
        var options = new RedemptionsOptions { FulfillmentSweepIntervalMinutes = minutes };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("FulfillmentSweepIntervalMinutes"));
    }

    /// <summary>
    /// The defaults must pass on their own: they are what a deployment gets when the section is
    /// absent entirely, and startup validation would otherwise refuse to boot.
    /// </summary>
    [Fact]
    public void Default_options_are_valid()
    {
        var result = _validator.Validate(null, new RedemptionsOptions());

        result.Succeeded.Should().BeTrue();
    }
}
