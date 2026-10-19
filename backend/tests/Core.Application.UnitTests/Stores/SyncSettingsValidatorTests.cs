using FluentAssertions;
using PointLurker.Core.Application.Stores;

namespace Core.Application.UnitTests.Stores;

public sealed class SyncSettingsValidatorTests
{
    private readonly SyncSettingsValidator _validator = new();

    [Fact]
    public void Valid_cron_expression_returns_success()
    {
        var settings = new SyncSettings { FetchCronExpression = "* * * * *" };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Null_cron_expression_returns_failure()
    {
        var settings = new SyncSettings { FetchCronExpression = null };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeFalse();
        result
            .Failures.Should()
            .ContainSingle(f => f.Contains("FetchCronExpression") && f.Contains("required"));
    }

    [Fact]
    public void Empty_cron_expression_returns_failure()
    {
        var settings = new SyncSettings { FetchCronExpression = "" };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeFalse();
        result
            .Failures.Should()
            .ContainSingle(f => f.Contains("FetchCronExpression") && f.Contains("required"));
    }

    [Fact]
    public void Whitespace_cron_expression_returns_failure()
    {
        var settings = new SyncSettings { FetchCronExpression = "   " };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeFalse();
        result
            .Failures.Should()
            .ContainSingle(f => f.Contains("FetchCronExpression") && f.Contains("required"));
    }

    [Theory]
    [InlineData("not-a-cron")]
    [InlineData("60 * * * *")]
    [InlineData("* * * * * * *")]
    public void Invalid_cron_syntax_returns_failure_with_expression_in_message(string invalidCron)
    {
        var settings = new SyncSettings { FetchCronExpression = invalidCron };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeFalse();
        result
            .Failures.Should()
            .ContainSingle(f => f.Contains("FetchCronExpression") && f.Contains(invalidCron));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_retention_returns_failure(int days)
    {
        var settings = new SyncSettings { ResolutionLogRetentionDays = days };

        var result = _validator.Validate(null, settings);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Contains("ResolutionLogRetentionDays"));
    }

    [Fact]
    public void Default_retention_is_thirty_days_and_valid()
    {
        var settings = new SyncSettings();

        settings.ResolutionLogRetentionDays.Should().Be(30);
        _validator.Validate(null, settings).Succeeded.Should().BeTrue();
    }
}
